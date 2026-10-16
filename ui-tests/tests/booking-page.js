const { expect } = require('@playwright/test');

class BookingPage {
  constructor(page) {
    this.page = page;
    this.apiKey = page.getByTestId('api-key');
    this.signIn = page.getByTestId('sign-in');
    this.signOut = page.getByTestId('sign-out');
    this.sessionUser = page.getByTestId('session-user');
    this.message = page.getByTestId('message');
    this.workspace = page.getByTestId('workspace');
    this.station = page.getByTestId('station');
    this.purpose = page.getByTestId('purpose');
    this.startsAt = page.getByTestId('starts-at');
    this.endsAt = page.getByTestId('ends-at');
    this.priority = page.getByTestId('priority');
    this.submit = page.getByTestId('submit-booking');
    this.stateFilter = page.getByTestId('state-filter');
    this.refresh = page.getByTestId('refresh');
    this.rows = page.getByTestId('booking-row');
    this.emptyState = page.getByTestId('empty-state');
  }

  async open() {
    await this.page.request.post('/api/test/reset');
    await this.page.goto('/');
  }

  async signInAs(key) {
    await this.apiKey.fill(key);
    await this.signIn.click();
    await expect(this.sessionUser).not.toHaveText('not signed in');
  }

  async fillBooking({ station = 'PER', purpose = 'Scheduled housekeeping pass',
                      leadMinutes = 120, durationMinutes = 30, priority = 'ROUTINE' } = {}) {
    const starts = new Date(Date.now() + leadMinutes * 60000);
    const ends = new Date(starts.getTime() + durationMinutes * 60000);
    await this.station.selectOption(station);
    await this.purpose.fill(purpose);
    await this.startsAt.fill(BookingPage.toLocalInput(starts));
    await this.endsAt.fill(BookingPage.toLocalInput(ends));
    await this.priority.selectOption(priority);
  }

  async submitBooking(options) {
    await this.fillBooking(options);
    await this.submit.click();
  }

  rowFor(purpose) {
    return this.rows.filter({ hasText: purpose });
  }

  static toLocalInput(date) {
    const offset = date.getTimezoneOffset() * 60000;
    return new Date(date.getTime() - offset).toISOString().slice(0, 16);
  }
}

module.exports = { BookingPage };
