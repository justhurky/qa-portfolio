const { test, expect } = require('@playwright/test');
const { BookingPage } = require('./booking-page');

test.describe('Ground station booking interface', () => {
  let booking;

  test.beforeEach(async ({ page }) => {
    booking = new BookingPage(page);
    await booking.open();
  });

  test('UI-01 the workspace stays hidden until a key is accepted', async () => {
    await expect(booking.workspace).toBeHidden();
    await expect(booking.sessionUser).toHaveText('not signed in');
  });

  test('UI-02 an unknown key is reported and keeps the workspace hidden', async () => {
    await booking.apiKey.fill('there-is-no-such-key');
    await booking.signIn.click();

    await expect(booking.message).toHaveText('Unknown API key');
    await expect(booking.message).toHaveAttribute('data-kind', 'error');
    await expect(booking.workspace).toBeHidden();
  });

  test('UI-03 an empty key is refused before a request is sent', async () => {
    await booking.signIn.click();

    await expect(booking.message).toHaveText('Enter an API key first.');
    await expect(booking.workspace).toBeHidden();
  });

  test('UI-04 an operator can sign in and see their role', async () => {
    await booking.signInAs('operator-key');

    await expect(booking.sessionUser).toHaveText('olga.operator (OPERATOR)');
    await expect(booking.workspace).toBeVisible();
    await expect(booking.submit).toBeEnabled();
  });

  test('UI-05 a viewer sees a read only workspace', async () => {
    await booking.signInAs('viewer-key');

    await expect(booking.message).toContainText('read only');
    await expect(booking.submit).toBeDisabled();
  });

  test('UI-06 only active stations are offered', async () => {
    await booking.signInAs('operator-key');

    const codes = await booking.station.locator('option').allTextContents();
    expect(codes.join(' ')).toContain('KIR');
    expect(codes.join(' ')).not.toContain('OLD');
  });

  test('UI-07 a valid booking appears in the table as REQUESTED', async () => {
    await booking.signInAs('operator-key');

    await booking.submitBooking({ purpose: 'Pass over Kiruna' });

    await expect(booking.message).toHaveText('Booking requested.');
    const row = booking.rowFor('Pass over Kiruna');
    await expect(row).toHaveCount(1);
    await expect(row.getByTestId('booking-state')).toHaveText('REQUESTED');
  });

  test('UI-08 the form is cleared after a successful request', async () => {
    await booking.signInAs('operator-key');
    await booking.submitBooking({ purpose: 'Clears the form' });

    await expect(booking.message).toHaveText('Booking requested.');
    await expect(booking.purpose).toHaveValue('');
  });

  test('UI-09 a short purpose is caught before the request is sent', async ({ page }) => {
    await booking.signInAs('operator-key');
    let requests = 0;
    page.on('request', (request) => {
      if (request.method() === 'POST' && request.url().includes('/api/bookings')) {
        requests += 1;
      }
    });

    await booking.submitBooking({ purpose: 'ab' });

    await expect(booking.message).toHaveText('Purpose must be at least 3 characters.');
    expect(requests).toBe(0);
  });

  test('UI-10 a server rejection is shown to the user', async () => {
    await booking.signInAs('operator-key');

    await booking.submitBooking({ purpose: 'Far too long a pass', durationMinutes: 240 });

    await expect(booking.message).toContainText('Duration must be between 5 and 120 minutes');
    await expect(booking.message).toHaveAttribute('data-kind', 'error');
  });

  test('UI-11 an operator may not request an emergency slot', async () => {
    await booking.signInAs('operator-key');

    await booking.submitBooking({ purpose: 'Emergency attempt', priority: 'EMERGENCY' });

    await expect(booking.message).toContainText('requires role SUPERVISOR');
  });

  test('UI-12 a supervisor can confirm a requested booking', async () => {
    await booking.signInAs('operator-key');
    await booking.submitBooking({ purpose: 'Needs approval' });
    await expect(booking.rowFor('Needs approval')).toHaveCount(1);

    await booking.signOut.click();
    await booking.signInAs('supervisor-key');

    const row = booking.rowFor('Needs approval');
    await row.getByTestId('action-confirm').click();

    await expect(row.getByTestId('booking-state')).toHaveText('CONFIRMED');
  });

  test('UI-13 an operator sees no confirm button on their own booking', async () => {
    await booking.signInAs('operator-key');
    await booking.submitBooking({ purpose: 'No approval rights' });

    const row = booking.rowFor('No approval rights');
    await expect(row.getByTestId('action-confirm')).toHaveCount(0);
    await expect(row.getByTestId('action-cancel')).toHaveCount(1);
  });

  test('UI-14 the state filter narrows the table', async () => {
    await booking.signInAs('supervisor-key');
    await booking.submitBooking({ purpose: 'Stays requested', leadMinutes: 120 });
    await booking.submitBooking({ purpose: 'Gets confirmed', leadMinutes: 300 });
    await booking.rowFor('Gets confirmed').getByTestId('action-confirm').click();
    await expect(booking.rowFor('Gets confirmed').getByTestId('booking-state'))
      .toHaveText('CONFIRMED');

    await booking.stateFilter.selectOption('CONFIRMED');

    await expect(booking.rows).toHaveCount(1);
    await expect(booking.rowFor('Gets confirmed')).toHaveCount(1);
  });

  test('UI-15 the empty state is shown when nothing matches', async () => {
    await booking.signInAs('operator-key');

    await booking.stateFilter.selectOption('COMPLETED');

    await expect(booking.emptyState).toBeVisible();
    await expect(booking.rows).toHaveCount(0);
  });

  test('UI-16 markup in a purpose is rendered as text and never executed', async ({ page }) => {
    const dialogs = [];
    page.on('dialog', (dialog) => {
      dialogs.push(dialog.message());
      dialog.dismiss();
    });
    await booking.signInAs('operator-key');

    await booking.submitBooking({ purpose: '<script>alert("xss")</script>' });

    const row = booking.rowFor('<script>alert("xss")</script>');
    await expect(row).toHaveCount(1);
    expect(dialogs).toHaveLength(0);
    expect(await row.locator('script').count()).toBe(0);
  });

  test('UI-17 signing out hides the workspace and clears the key', async () => {
    await booking.signInAs('operator-key');

    await booking.signOut.click();

    await expect(booking.workspace).toBeHidden();
    await expect(booking.apiKey).toHaveValue('');
    await expect(booking.sessionUser).toHaveText('not signed in');
  });

  test('UI-18 a capacity clash is surfaced as an error', async () => {
    await booking.signInAs('operator-key');
    await booking.submitBooking({ station: 'KIR', purpose: 'First pass', leadMinutes: 120 });
    await expect(booking.message).toHaveText('Booking requested.');

    await booking.submitBooking({ station: 'KIR', purpose: 'Clashing pass', leadMinutes: 130 });

    await expect(booking.message).toContainText('overlapping booking');
  });
});
