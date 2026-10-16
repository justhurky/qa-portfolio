import http from 'k6/http';
import { check, group } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const BASE_URL = __ENV.SUT_BASE_URL || 'http://localhost:3000';
const OPERATOR = { 'X-Api-Key': 'operator-key', 'Content-Type': 'application/json' };
const VIEWER = { 'X-Api-Key': 'viewer-key' };

const createDuration = new Trend('booking_create_duration', true);
const listDuration = new Trend('booking_list_duration', true);
const capacityRejections = new Rate('capacity_rejections');

export const options = {
  scenarios: {
    browsing: {
      executor: 'constant-vus',
      vus: 10,
      duration: '20s',
      exec: 'browse'
    },
    booking: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '5s', target: 5 },
        { duration: '10s', target: 5 },
        { duration: '5s', target: 0 }
      ],
      exec: 'book'
    }
  },
  thresholds: {
    'http_req_failed': ['rate<0.01'],
    'http_req_duration': ['p(95)<300', 'p(99)<800'],
    'booking_list_duration': ['p(95)<150'],
    'booking_create_duration': ['p(95)<250'],
    'checks': ['rate>0.99']
  }
};

export function setup() {
  const reset = http.post(`${BASE_URL}/api/test/reset`);
  check(reset, { 'system under test was reset': (r) => r.status === 200 });
}

export function browse() {
  group('read the schedule', () => {
    const stations = http.get(`${BASE_URL}/api/stations?activeOnly=true`, { headers: VIEWER });
    check(stations, {
      'stations answered 200': (r) => r.status === 200,
      'stations came back as a page': (r) => r.json('content') !== undefined
    });

    const bookings = http.get(`${BASE_URL}/api/bookings?size=50`, { headers: VIEWER });
    listDuration.add(bookings.timings.duration);
    check(bookings, { 'bookings answered 200': (r) => r.status === 200 });
  });
}

export function book() {
  const offsetMinutes = 60 + Math.floor(Math.random() * 20000);
  const startsAt = new Date(Date.now() + offsetMinutes * 60000).toISOString();
  const endsAt = new Date(Date.now() + (offsetMinutes + 30) * 60000).toISOString();

  const response = http.post(`${BASE_URL}/api/bookings`, JSON.stringify({
    stationCode: 'SVB',
    purpose: `Load generated pass ${__VU}-${__ITER}`,
    startsAt,
    endsAt
  }), { headers: OPERATOR });

  createDuration.add(response.timings.duration);
  capacityRejections.add(response.status === 409);

  check(response, {
    'booking was answered deterministically': (r) => r.status === 201 || r.status === 409,
    'a rejection explains itself': (r) => r.status !== 409 || r.json('error.code') === 'STATION_AT_CAPACITY'
  });
}
