const state = { apiKey: null, user: null };

const elements = {
  apiKey: document.getElementById('api-key'),
  signIn: document.getElementById('sign-in'),
  signOut: document.getElementById('sign-out'),
  sessionUser: document.getElementById('session-user'),
  message: document.getElementById('message'),
  workspace: document.getElementById('workspace'),
  form: document.getElementById('booking-form'),
  station: document.getElementById('station'),
  purpose: document.getElementById('purpose'),
  startsAt: document.getElementById('starts-at'),
  endsAt: document.getElementById('ends-at'),
  priority: document.getElementById('priority'),
  submit: document.getElementById('submit-booking'),
  stateFilter: document.getElementById('state-filter'),
  refresh: document.getElementById('refresh'),
  body: document.getElementById('bookings-body'),
  emptyState: document.getElementById('empty-state')
};

function showMessage(text, kind) {
  elements.message.textContent = text;
  elements.message.dataset.kind = kind;
  elements.message.hidden = false;
}

function clearMessage() {
  elements.message.hidden = true;
  elements.message.textContent = '';
  delete elements.message.dataset.kind;
}

async function call(method, url, body) {
  const options = { method, headers: { 'X-Api-Key': state.apiKey || '' } };
  if (body !== undefined) {
    options.headers['Content-Type'] = 'application/json';
    options.body = JSON.stringify(body);
  }
  const response = await fetch(url, options);
  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;
  if (!response.ok) {
    const error = new Error(payload && payload.error ? payload.error.message : 'Request failed');
    error.code = payload && payload.error ? payload.error.code : 'UNKNOWN';
    throw error;
  }
  return payload;
}

function toIsoOrNull(value) {
  if (!value) {
    return null;
  }
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

function formatWindow(booking) {
  const starts = new Date(booking.startsAt);
  const ends = new Date(booking.endsAt);
  const minutes = Math.round((ends - starts) / 60000);
  return `${starts.toISOString().slice(0, 16).replace('T', ' ')} (${minutes} min)`;
}

function actionsFor(booking) {
  const actions = [];
  const isSupervisor = state.user.role === 'SUPERVISOR' || state.user.role === 'ADMIN';
  const owns = booking.requestedBy === state.user.username;
  if (booking.state === 'REQUESTED' && isSupervisor) {
    actions.push({ label: 'Confirm', path: 'confirm' });
  }
  if (booking.state === 'CONFIRMED' && isSupervisor) {
    actions.push({ label: 'Complete', path: 'complete' });
  }
  if ((booking.state === 'REQUESTED' || booking.state === 'CONFIRMED') && (owns || isSupervisor)) {
    actions.push({ label: 'Cancel', path: 'cancel' });
  }
  return actions;
}

function renderBookings(bookings) {
  elements.body.replaceChildren();
  elements.emptyState.hidden = bookings.length > 0;

  bookings.forEach((booking) => {
    const row = document.createElement('tr');
    row.dataset.testid = 'booking-row';
    row.dataset.bookingId = booking.id;
    row.dataset.state = booking.state;

    const cells = [
      booking.stationCode,
      booking.purpose,
      formatWindow(booking),
      booking.priority,
      booking.state,
      booking.requestedBy
    ];
    cells.forEach((value, column) => {
      const cell = document.createElement('td');
      cell.textContent = value;
      if (column === 4) {
        cell.dataset.testid = 'booking-state';
      }
      row.appendChild(cell);
    });

    const actionCell = document.createElement('td');
    actionsFor(booking).forEach((action) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = action.label;
      button.dataset.testid = `action-${action.path}`;
      button.addEventListener('click', () => transition(booking.id, action.path));
      actionCell.appendChild(button);
    });
    row.appendChild(actionCell);
    elements.body.appendChild(row);
  });
}

async function loadBookings() {
  const filter = elements.stateFilter.value;
  const query = filter ? `?state=${encodeURIComponent(filter)}&size=100` : '?size=100';
  const page = await call('GET', `/api/bookings${query}`);
  renderBookings(page.content);
}

async function loadStations() {
  const page = await call('GET', '/api/stations?activeOnly=true&size=100');
  elements.station.replaceChildren();
  page.content.forEach((station) => {
    const option = document.createElement('option');
    option.value = station.code;
    option.textContent = `${station.code} — ${station.name}`;
    elements.station.appendChild(option);
  });
}

async function transition(id, path) {
  clearMessage();
  try {
    await call('POST', `/api/bookings/${id}/${path}`);
    showMessage(`Booking ${path}ed.`, 'success');
    await loadBookings();
  } catch (error) {
    showMessage(error.message, 'error');
  }
}

async function signIn() {
  clearMessage();
  state.apiKey = elements.apiKey.value.trim();
  if (!state.apiKey) {
    showMessage('Enter an API key first.', 'error');
    return;
  }
  try {
    state.user = await call('GET', '/api/me');
    elements.sessionUser.textContent = `${state.user.username} (${state.user.role})`;
    elements.workspace.hidden = false;
    elements.signOut.hidden = false;
    elements.submit.disabled = state.user.role === 'VIEWER';
    if (state.user.role === 'VIEWER') {
      showMessage('Signed in as a viewer: bookings are read only.', 'info');
    }
    await loadStations();
    await loadBookings();
  } catch (error) {
    state.apiKey = null;
    state.user = null;
    showMessage(error.message, 'error');
  }
}

function signOut() {
  state.apiKey = null;
  state.user = null;
  elements.sessionUser.textContent = 'not signed in';
  elements.workspace.hidden = true;
  elements.signOut.hidden = true;
  elements.apiKey.value = '';
  clearMessage();
}

async function submitBooking(event) {
  event.preventDefault();
  clearMessage();

  const purpose = elements.purpose.value.trim();
  if (purpose.length < 3) {
    showMessage('Purpose must be at least 3 characters.', 'error');
    return;
  }
  const startsAt = toIsoOrNull(elements.startsAt.value);
  const endsAt = toIsoOrNull(elements.endsAt.value);
  if (!startsAt || !endsAt) {
    showMessage('Both start and end time are required.', 'error');
    return;
  }

  try {
    await call('POST', '/api/bookings', {
      stationCode: elements.station.value,
      purpose,
      startsAt,
      endsAt,
      priority: elements.priority.value
    });
    showMessage('Booking requested.', 'success');
    elements.form.reset();
    await loadBookings();
  } catch (error) {
    showMessage(error.message, 'error');
  }
}

elements.signIn.addEventListener('click', signIn);
elements.signOut.addEventListener('click', signOut);
elements.form.addEventListener('submit', submitBooking);
elements.refresh.addEventListener('click', () => loadBookings().catch((error) => showMessage(error.message, 'error')));
elements.stateFilter.addEventListener('change', () => loadBookings().catch((error) => showMessage(error.message, 'error')));
