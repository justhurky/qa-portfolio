const crypto = require('crypto');

const SEED_STATIONS = [
  { code: 'KIR', name: 'Kiruna', country: 'SE', capacity: 1, active: true },
  { code: 'SVB', name: 'Svalbard', country: 'NO', capacity: 2, active: true },
  { code: 'PER', name: 'Perth', country: 'AU', capacity: 1, active: true },
  { code: 'OLD', name: 'Retired Dish', country: 'HU', capacity: 1, active: false }
];

const SEED_USERS = [
  { apiKey: 'viewer-key', username: 'vera.viewer', role: 'VIEWER' },
  { apiKey: 'operator-key', username: 'olga.operator', role: 'OPERATOR' },
  { apiKey: 'operator2-key', username: 'oscar.operator', role: 'OPERATOR' },
  { apiKey: 'supervisor-key', username: 'sam.supervisor', role: 'SUPERVISOR' },
  { apiKey: 'admin-key', username: 'ada.admin', role: 'ADMIN' }
];

class Store {
  constructor() {
    this.reset();
  }

  reset() {
    this.stations = SEED_STATIONS.map((station) => ({ ...station }));
    this.users = SEED_USERS.map((user) => ({ ...user }));
    this.bookings = [];
  }

  findUserByApiKey(apiKey) {
    return this.users.find((user) => user.apiKey === apiKey) || null;
  }

  listStations({ activeOnly = false } = {}) {
    return this.stations
      .filter((station) => (activeOnly ? station.active : true))
      .map((station) => ({ ...station }));
  }

  findStation(code) {
    if (typeof code !== 'string') {
      return null;
    }
    const match = this.stations.find(
      (station) => station.code.toUpperCase() === code.toUpperCase()
    );
    return match ? { ...match } : null;
  }

  addStation(station) {
    this.stations.push({ ...station });
    return { ...station };
  }

  listBookings({ stationCode, state, requestedBy } = {}) {
    return this.bookings
      .filter((booking) => (stationCode ? booking.stationCode === stationCode.toUpperCase() : true))
      .filter((booking) => (state ? booking.state === state : true))
      .filter((booking) => (requestedBy ? booking.requestedBy === requestedBy : true))
      .map((booking) => ({ ...booking }));
  }

  findBooking(id) {
    const match = this.bookings.find((booking) => booking.id === id);
    return match ? { ...match } : null;
  }

  activeBookingsForStation(stationCode) {
    return this.bookings.filter(
      (booking) =>
        booking.stationCode === stationCode.toUpperCase() &&
        (booking.state === 'REQUESTED' || booking.state === 'CONFIRMED')
    );
  }

  addBooking(booking) {
    const stored = { id: crypto.randomUUID(), ...booking };
    this.bookings.push(stored);
    return { ...stored };
  }

  updateBooking(id, changes) {
    const index = this.bookings.findIndex((booking) => booking.id === id);
    if (index < 0) {
      return null;
    }
    this.bookings[index] = { ...this.bookings[index], ...changes };
    return { ...this.bookings[index] };
  }
}

module.exports = { Store, SEED_STATIONS, SEED_USERS };
