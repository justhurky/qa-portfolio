using GroundStation.UiTests.Pages;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace GroundStation.UiTests;

[TestFixture]
public class BookingUiTests
{
    private SystemUnderTest sut = null!;
    private IWebDriver driver = null!;
    private WebDriverWait wait = null!;
    private BookingPage page = null!;

    [OneTimeSetUp]
    public void StartBrowser()
    {
        sut = SystemUnderTest.Start();

        ChromeOptions options = new();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--window-size=1280,900");

        driver = new ChromeDriver(options);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
        {
            PollingInterval = TimeSpan.FromMilliseconds(100)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        page = new BookingPage(driver, wait, sut.BaseUrl);
    }

    [SetUp]
    public async Task ResetAndOpen()
    {
        await sut.ResetAsync();
        page.Open();
    }

    [OneTimeTearDown]
    public void StopBrowser()
    {
        driver?.Quit();
        driver?.Dispose();
        sut?.Dispose();
    }

    [Test(Description = "UI-01 the workspace stays hidden until a key is accepted")]
    public void WorkspaceIsHiddenBeforeSignIn()
    {
        Assert.That(page.WorkspaceVisible(), Is.False);
        Assert.That(page.SessionUser.Text, Is.EqualTo("not signed in"));
    }

    [Test(Description = "UI-02 an unknown key is reported and the workspace stays hidden")]
    public void UnknownKeyIsReported()
    {
        page.ApiKey.SendKeys("there-is-no-such-key");
        page.SignIn.Click();

        Assert.That(page.MessageText(), Is.EqualTo("Unknown API key"));
        Assert.That(page.MessageKind(), Is.EqualTo("error"));
        Assert.That(page.WorkspaceVisible(), Is.False);
    }

    [Test(Description = "UI-03 an empty key is refused before any request is sent")]
    public void EmptyKeyIsRefusedLocally()
    {
        page.SignIn.Click();

        Assert.That(page.MessageText(), Is.EqualTo("Enter an API key first."));
        Assert.That(page.WorkspaceVisible(), Is.False);
    }

    [Test(Description = "UI-04 an operator signs in and sees their role")]
    public void OperatorSeesTheirRole()
    {
        page.SignInAs("operator-key");

        Assert.That(page.SessionUser.Text, Is.EqualTo("olga.operator (OPERATOR)"));
        Assert.That(page.WorkspaceVisible(), Is.True);
        Assert.That(page.Submit.Enabled, Is.True);
    }

    [Test(Description = "UI-05 a viewer sees a read only workspace")]
    public void ViewerIsReadOnly()
    {
        page.SignInAs("viewer-key");

        Assert.That(page.MessageText(), Does.Contain("read only"));
        Assert.That(page.Submit.Enabled, Is.False);
    }

    [Test(Description = "UI-06 only active stations are offered")]
    public void OnlyActiveStationsAreOffered()
    {
        page.SignInAs("operator-key");

        List<string> codes = page.Station.Options.Select(option => option.GetDomAttribute("value") ?? "")
            .ToList();

        Assert.That(codes, Does.Contain("KIR"));
        Assert.That(codes, Does.Not.Contain("OLD"));
    }

    [Test(Description = "UI-07 a valid booking appears in the table as REQUESTED")]
    public void ValidBookingAppearsAsRequested()
    {
        page.SignInAs("operator-key");

        page.SubmitBooking("Pass over Perth");

        Assert.That(page.MessageText(), Is.EqualTo("Booking requested."));
        Assert.That(page.StateOf("Pass over Perth"), Is.EqualTo("REQUESTED"));
    }

    [Test(Description = "UI-08 the form is cleared after a successful request")]
    public void FormIsClearedAfterSuccess()
    {
        page.SignInAs("operator-key");

        page.SubmitBooking("Clears the form");

        Assert.That(page.MessageText(), Is.EqualTo("Booking requested."));
        Assert.That(page.Purpose.GetDomProperty("value"), Is.Empty);
    }

    [Test(Description = "UI-09 a short purpose is caught before the request is sent")]
    public void ShortPurposeIsCaughtLocally()
    {
        page.SignInAs("operator-key");

        page.SubmitBooking("ab");

        Assert.That(page.MessageText(), Is.EqualTo("Purpose must be at least 3 characters."));
        Assert.That(page.Rows, Is.Empty);
    }

    [Test(Description = "UI-10 a server rejection is shown to the user")]
    public void ServerRejectionIsShown()
    {
        page.SignInAs("operator-key");

        page.SubmitBooking("Far too long a pass", durationMinutes: 240);

        Assert.That(page.MessageText(), Does.Contain("Duration must be between 5 and 120 minutes"));
        Assert.That(page.MessageKind(), Is.EqualTo("error"));
    }

    [Test(Description = "UI-11 an operator may not request an emergency slot")]
    public void OperatorMayNotRequestEmergency()
    {
        page.SignInAs("operator-key");

        page.SubmitBooking("Emergency attempt", priority: "EMERGENCY");

        Assert.That(page.MessageText(), Does.Contain("requires role Supervisor"));
    }

    [Test(Description = "UI-12 a supervisor can confirm a requested booking")]
    public void SupervisorCanConfirm()
    {
        page.SignInAs("operator-key");
        page.SubmitBooking("Needs approval");
        page.WaitForState("Needs approval", "REQUESTED");
        page.SignOut.Click();
        page.SignInAs("supervisor-key");

        page.Act("Needs approval", "confirm");

        page.WaitForState("Needs approval", "CONFIRMED");
        Assert.That(page.StateOf("Needs approval"), Is.EqualTo("CONFIRMED"));
    }

    [Test(Description = "UI-13 an operator sees no confirm button on their own booking")]
    public void OperatorSeesNoConfirmButton()
    {
        page.SignInAs("operator-key");
        page.SubmitBooking("No approval rights");
        page.WaitForState("No approval rights", "REQUESTED");

        Assert.That(page.HasAction("No approval rights", "confirm"), Is.False);
        Assert.That(page.HasAction("No approval rights", "cancel"), Is.True);
    }

    [Test(Description = "UI-14 the state filter narrows the table")]
    public void StateFilterNarrowsTheTable()
    {
        page.SignInAs("supervisor-key");
        page.SubmitBooking("Stays requested", leadMinutes: 120);
        page.WaitForState("Stays requested", "REQUESTED");
        page.SubmitBooking("Gets confirmed", leadMinutes: 300);
        page.WaitForState("Gets confirmed", "REQUESTED");
        page.Act("Gets confirmed", "confirm");
        page.WaitForState("Gets confirmed", "CONFIRMED");

        page.StateFilter.SelectByValue("CONFIRMED");

        wait.Until(_ => page.Rows.Count == 1);
        Assert.That(page.StateOf("Gets confirmed"), Is.EqualTo("CONFIRMED"));
    }

    [Test(Description = "UI-15 the empty state is shown when nothing matches")]
    public void EmptyStateIsShown()
    {
        page.SignInAs("operator-key");

        page.StateFilter.SelectByValue("COMPLETED");

        wait.Until(_ => page.EmptyState.Displayed);
        Assert.That(page.Rows, Is.Empty);
    }

    [Test(Description = "UI-16 markup in a purpose is rendered as text and never executed")]
    public void MarkupIsRenderedAsText()
    {
        page.SignInAs("operator-key");
        const string payload = "<script>alert('xss')</script>";

        page.SubmitBooking(payload);

        IWebElement row = page.RowFor(payload);
        Assert.That(row.FindElements(By.TagName("script")), Is.Empty);
        Assert.That(() => driver.SwitchTo().Alert(), Throws.TypeOf<NoAlertPresentException>());
    }

    [Test(Description = "UI-17 signing out hides the workspace and clears the key")]
    public void SigningOutClearsTheSession()
    {
        page.SignInAs("operator-key");

        page.SignOut.Click();

        Assert.That(page.WorkspaceVisible(), Is.False);
        Assert.That(page.ApiKey.GetDomProperty("value"), Is.Empty);
        Assert.That(page.SessionUser.Text, Is.EqualTo("not signed in"));
    }

    [Test(Description = "UI-18 a capacity clash is surfaced as an error")]
    public void CapacityClashIsSurfaced()
    {
        page.SignInAs("operator-key");
        page.SubmitBooking("First pass", station: "KIR", leadMinutes: 120);
        page.WaitForState("First pass", "REQUESTED");

        page.SubmitBooking("Clashing pass", station: "KIR", leadMinutes: 130);

        Assert.That(page.MessageText(), Does.Contain("overlapping booking"));
    }

    [Test(Description = "UI-19 a cancelled booking keeps its row and loses its actions")]
    public void CancelledBookingLosesItsActions()
    {
        page.SignInAs("operator-key");
        page.SubmitBooking("Will be cancelled");
        page.WaitForState("Will be cancelled", "REQUESTED");

        page.Act("Will be cancelled", "cancel");

        page.WaitForState("Will be cancelled", "CANCELLED");
        Assert.That(page.HasAction("Will be cancelled", "cancel"), Is.False);
    }
}
