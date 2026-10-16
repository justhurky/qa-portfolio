const http = require('http');
const fs = require('fs');
const path = require('path');
const domain = require('./domain');
const { Store } = require('./store');

const PUBLIC_DIR = path.join(__dirname, 'public');
const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.ico': 'image/x-icon'
};

const store = new Store();
const allowReset = process.env.SUT_ALLOW_RESET !== 'false';

class ApiError extends Error {
  constructor(status, code, message, details) {
    super(message);
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

function sendJson(response, status, body) {
  const payload = JSON.stringify(body);
  response.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(payload),
    'X-Content-Type-Options': 'nosniff'
  });
  response.end(payload);
}

function sendError(response, error) {
  const status = error.status || 500;
  const body = {
    error: {
      code: error.code || 'INTERNAL_ERROR',
      message: error.status ? error.message : 'Unexpected server error'
    }
  };
  if (error.details) {
    body.error.details = error.details;
  }
  sendJson(response, status, body);
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    request.on('data', (chunk) => {
      size += chunk.length;
      if (size > 64 * 1024) {
        reject(new ApiError(413, 'PAYLOAD_TOO_LARGE', 'Request body is larger than 64 KiB'));
        request.destroy();
        return;
      }
      chunks.push(chunk);
    });
    request.on('end', () => {
      const raw = Buffer.concat(chunks).toString('utf8');
      if (raw.trim() === '') {
        resolve({});
        return;
      }
      try {
        const parsed = JSON.parse(raw);
        if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
          reject(new ApiError(400, 'MALFORMED_JSON', 'Request body must be a JSON object'));
          return;
        }
        resolve(parsed);
      } catch (parseError) {
        reject(new ApiError(400, 'MALFORMED_JSON', 'Request body is not valid JSON'));
      }
    });
    request.on('error', reject);
  });
}

function authenticate(request) {
  const apiKey = request.headers['x-api-key'];
  if (!apiKey) {
    throw new ApiError(401, 'AUTHENTICATION_REQUIRED', 'X-Api-Key header is required');
  }
  const user = store.findUserByApiKey(Array.isArray(apiKey) ? apiKey[0] : apiKey);
  if (!user) {
    throw new ApiError(401, 'AUTHENTICATION_FAILED', 'Unknown API key');
  }
  return user;
}

function requireRole(user, minimumRole) {
  if (!domain.hasAtLeastRole(user.role, minimumRole)) {
    throw new ApiError(403, 'ACCESS_DENIED', `Role ${minimumRole} or higher is required`);
  }
}

function requireString(body, field, { min, max }) {
  const value = body[field];
  if (typeof value !== 'string' || value.trim() === '') {
    throw new ApiError(400, 'VALIDATION_FAILED', `${field} is required`, { field });
  }
  const trimmed = value.trim();
  if (trimmed.length < min || trimmed.length > max) {
    throw new ApiError(400, 'VALIDATION_FAILED',
      `${field} must be between ${min} and ${max} characters`, { field, length: trimmed.length });
  }
  return trimmed;
}

function requireInstant(body, field) {
  const parsed = domain.parseInstant(body[field]);
  if (!parsed) {
    throw new ApiError(400, 'VALIDATION_FAILED', `${field} must be an ISO-8601 timestamp`, { field });
  }
  return parsed;
}

function requirePriority(body) {
  const value = body.priority === undefined ? 'ROUTINE' : body.priority;
  if (!domain.PRIORITIES.includes(value)) {
    throw new ApiError(400, 'VALIDATION_FAILED',
      `priority must be one of ${domain.PRIORITIES.join(', ')}`, { field: 'priority' });
  }
  return value;
}

function paginate(items, query) {
  const page = Number.parseInt(query.get('page') || '0', 10);
  const size = Number.parseInt(query.get('size') || '20', 10);
  if (!Number.isInteger(page) || page < 0) {
    throw new ApiError(400, 'VALIDATION_FAILED', 'page must be a non-negative integer',
      { field: 'page' });
  }
  if (!Number.isInteger(size) || size < 1 || size > 100) {
    throw new ApiError(400, 'VALIDATION_FAILED', 'size must be between 1 and 100', { field: 'size' });
  }
  const from = page * size;
  return {
    content: items.slice(from, from + size),
    page,
    size,
    totalElements: items.length,
    totalPages: Math.ceil(items.length / size)
  };
}

function createBooking(user, body, now) {
  requireRole(user, 'OPERATOR');

  const stationCode = requireString(body, 'stationCode', { min: 2, max: 16 }).toUpperCase();
  const purpose = requireString(body, 'purpose',
    { min: domain.MIN_PURPOSE_LENGTH, max: domain.MAX_PURPOSE_LENGTH });
  const startsAt = requireInstant(body, 'startsAt');
  const endsAt = requireInstant(body, 'endsAt');
  const priority = requirePriority(body);

  const rule = domain.PRIORITY_RULES[priority];
  if (!domain.hasAtLeastRole(user.role, rule.minimumRole)) {
    throw new ApiError(403, 'PRIORITY_NOT_PERMITTED',
      `Priority ${priority} requires role ${rule.minimumRole} or higher`);
  }

  const station = store.findStation(stationCode);
  if (!station) {
    throw new ApiError(404, 'STATION_NOT_FOUND', `No station with code ${stationCode}`);
  }
  if (!station.active) {
    throw new ApiError(409, 'STATION_INACTIVE', `Station ${stationCode} is not accepting bookings`);
  }

  if (endsAt <= startsAt) {
    throw new ApiError(400, 'VALIDATION_FAILED', 'endsAt must be after startsAt',
      { field: 'endsAt' });
  }

  const minutes = domain.durationMinutes(startsAt, endsAt);
  if (minutes < domain.MIN_DURATION_MINUTES || minutes > domain.MAX_DURATION_MINUTES) {
    throw new ApiError(422, 'DURATION_OUT_OF_RANGE',
      `Duration must be between ${domain.MIN_DURATION_MINUTES} and ${domain.MAX_DURATION_MINUTES} minutes`,
      { durationMinutes: minutes });
  }

  const lead = domain.leadMinutes(startsAt, now);
  if (!rule.mayOverrideLeadTime && lead < domain.MIN_LEAD_MINUTES) {
    throw new ApiError(422, 'LEAD_TIME_TOO_SHORT',
      `A ${priority} booking must start at least ${domain.MIN_LEAD_MINUTES} minutes from now`,
      { leadMinutes: Math.floor(lead) });
  }
  if (lead > domain.MAX_LEAD_DAYS * 24 * 60) {
    throw new ApiError(422, 'LEAD_TIME_TOO_LONG',
      `A booking may not start more than ${domain.MAX_LEAD_DAYS} days from now`,
      { leadMinutes: Math.floor(lead) });
  }

  const candidate = { startsAt, endsAt };
  const clashes = store
    .activeBookingsForStation(stationCode)
    .filter((booking) =>
      domain.overlaps(candidate, {
        startsAt: new Date(booking.startsAt),
        endsAt: new Date(booking.endsAt)
      }));
  if (clashes.length >= station.capacity) {
    throw new ApiError(409, 'STATION_AT_CAPACITY',
      `Station ${stationCode} already has ${clashes.length} overlapping booking(s)`,
      { capacity: station.capacity });
  }

  return store.addBooking({
    stationCode,
    purpose,
    priority,
    state: 'REQUESTED',
    startsAt: startsAt.toISOString(),
    endsAt: endsAt.toISOString(),
    requestedBy: user.username,
    requestedAt: now.toISOString(),
    decidedBy: null,
    decidedAt: null
  });
}

function transitionBooking(user, id, target, now) {
  const booking = store.findBooking(id);
  if (!booking) {
    throw new ApiError(404, 'BOOKING_NOT_FOUND', `No booking with id ${id}`);
  }

  if (target === 'CANCELLED') {
    const ownsBooking = booking.requestedBy === user.username;
    if (!ownsBooking && !domain.hasAtLeastRole(user.role, 'SUPERVISOR')) {
      throw new ApiError(403, 'ACCESS_DENIED',
        'Only the requester or a supervisor may cancel this booking');
    }
    requireRole(user, 'OPERATOR');
  } else {
    requireRole(user, 'SUPERVISOR');
  }

  if (!domain.canTransition(booking.state, target)) {
    throw new ApiError(409, 'ILLEGAL_STATE_TRANSITION',
      `A booking in state ${booking.state} cannot move to ${target}`,
      { from: booking.state, to: target, allowed: domain.ALLOWED_TRANSITIONS[booking.state] });
  }

  return store.updateBooking(id, {
    state: target,
    decidedBy: user.username,
    decidedAt: now.toISOString()
  });
}

function serveStatic(request, response) {
  const url = new URL(request.url, 'http://localhost');
  const requested = url.pathname === '/' ? '/index.html' : url.pathname;
  const resolved = path.join(PUBLIC_DIR, path.normalize(requested).replace(/^(\.\.[/\\])+/, ''));
  if (!resolved.startsWith(PUBLIC_DIR)) {
    sendError(response, new ApiError(403, 'ACCESS_DENIED', 'Path is outside the public directory'));
    return;
  }
  fs.readFile(resolved, (error, content) => {
    if (error) {
      sendError(response, new ApiError(404, 'NOT_FOUND', 'No such resource'));
      return;
    }
    response.writeHead(200, { 'Content-Type': MIME_TYPES[path.extname(resolved)] || 'text/plain' });
    response.end(content);
  });
}

async function route(request, response) {
  const url = new URL(request.url, 'http://localhost');
  const segments = url.pathname.split('/').filter(Boolean);
  const now = new Date();

  if (segments[0] !== 'api') {
    serveStatic(request, response);
    return;
  }

  if (request.method === 'GET' && segments[1] === 'health' && segments.length === 2) {
    sendJson(response, 200, { status: 'UP', time: now.toISOString() });
    return;
  }

  if (request.method === 'POST' && segments[1] === 'test' && segments[2] === 'reset') {
    if (!allowReset) {
      throw new ApiError(404, 'NOT_FOUND', 'No such resource');
    }
    store.reset();
    sendJson(response, 200, { status: 'RESET' });
    return;
  }

  const user = authenticate(request);

  if (request.method === 'GET' && segments[1] === 'me' && segments.length === 2) {
    sendJson(response, 200, { username: user.username, role: user.role });
    return;
  }

  if (segments[1] === 'stations') {
    if (request.method === 'GET' && segments.length === 2) {
      const activeOnly = url.searchParams.get('activeOnly') === 'true';
      sendJson(response, 200, paginate(store.listStations({ activeOnly }), url.searchParams));
      return;
    }
    if (request.method === 'GET' && segments.length === 3) {
      const station = store.findStation(segments[2]);
      if (!station) {
        throw new ApiError(404, 'STATION_NOT_FOUND', `No station with code ${segments[2]}`);
      }
      sendJson(response, 200, station);
      return;
    }
    if (request.method === 'POST' && segments.length === 2) {
      requireRole(user, 'ADMIN');
      const body = await readBody(request);
      const code = requireString(body, 'code', { min: 2, max: 16 }).toUpperCase();
      const name = requireString(body, 'name', { min: 2, max: 64 });
      if (store.findStation(code)) {
        throw new ApiError(409, 'STATION_ALREADY_EXISTS', `Station ${code} already exists`);
      }
      const capacity = body.capacity === undefined ? 1 : body.capacity;
      if (!Number.isInteger(capacity) || capacity < 1 || capacity > 8) {
        throw new ApiError(400, 'VALIDATION_FAILED', 'capacity must be an integer between 1 and 8',
          { field: 'capacity' });
      }
      sendJson(response, 201, store.addStation({
        code,
        name,
        country: typeof body.country === 'string' ? body.country : 'XX',
        capacity,
        active: body.active !== false
      }));
      return;
    }
  }

  if (segments[1] === 'bookings') {
    if (request.method === 'GET' && segments.length === 2) {
      const filtered = store.listBookings({
        stationCode: url.searchParams.get('stationCode') || undefined,
        state: url.searchParams.get('state') || undefined,
        requestedBy: url.searchParams.get('requestedBy') || undefined
      });
      filtered.sort((left, right) => left.startsAt.localeCompare(right.startsAt));
      sendJson(response, 200, paginate(filtered, url.searchParams));
      return;
    }
    if (request.method === 'POST' && segments.length === 2) {
      const body = await readBody(request);
      sendJson(response, 201, createBooking(user, body, now));
      return;
    }
    if (request.method === 'GET' && segments.length === 3) {
      const booking = store.findBooking(segments[2]);
      if (!booking) {
        throw new ApiError(404, 'BOOKING_NOT_FOUND', `No booking with id ${segments[2]}`);
      }
      sendJson(response, 200, booking);
      return;
    }
    if (request.method === 'POST' && segments.length === 4) {
      const targets = { confirm: 'CONFIRMED', cancel: 'CANCELLED', complete: 'COMPLETED' };
      const target = targets[segments[3]];
      if (!target) {
        throw new ApiError(404, 'NOT_FOUND', 'No such resource');
      }
      sendJson(response, 200, transitionBooking(user, segments[2], target, now));
      return;
    }
  }

  throw new ApiError(404, 'NOT_FOUND', 'No such resource');
}

const server = http.createServer((request, response) => {
  route(request, response).catch((error) => {
    if (!(error instanceof ApiError)) {
      process.stderr.write(`unhandled: ${error.stack}\n`);
    }
    if (!response.headersSent) {
      sendError(response, error);
    }
  });
});

const port = Number.parseInt(process.env.PORT || '3000', 10);

if (require.main === module) {
  server.listen(port, () => {
    process.stdout.write(`ground station booking api listening on ${port}\n`);
  });
}

module.exports = { server, store };
