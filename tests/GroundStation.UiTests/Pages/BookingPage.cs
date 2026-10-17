using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace GroundStation.UiTests.Pages;

public sealed class BookingPage(IWebDriver driver, WebDriverWait wait, string baseUrl)
{
    private static By Test(string id) => By.CssSelector($"[data-testid='{id}']");

    public IWebElement ApiKey => driver.FindElement(Test("api-key"));
    public IWebElement SignIn => driver.FindElement(Test("sign-in"));
    public IWebElement SignOut => driver.FindElement(Test("sign-out"));
    public IWebElement SessionUser => driver.FindElement(Test("session-user"));
    public IWebElement Message => driver.FindElement(Test("message"));
    public IWebElement Workspace => driver.FindElement(Test("workspace"));
    public IWebElement Purpose => driver.FindElement(Test("purpose"));
    public IWebElement Submit => driver.FindElement(Test("submit-booking"));
    public IWebElement Refresh => driver.FindElement(Test("refresh"));
    public IWebElement EmptyState => driver.FindElement(Test("empty-state"));

    public SelectElement Station => new(driver.FindElement(Test("station")));
    public SelectElement Priority => new(driver.FindElement(Test("priority")));
    public SelectElement StateFilter => new(driver.FindElement(Test("state-filter")));

    public IReadOnlyCollection<IWebElement> Rows => driver.FindElements(Test("booking-row"));

    public void Open() => driver.Navigate().GoToUrl(baseUrl + "/");

    public void SignInAs(string apiKey)
    {
        ApiKey.Clear();
        ApiKey.SendKeys(apiKey);
        SignIn.Click();
        wait.Until(_ => SessionUser.Text != "not signed in");
    }

    public string MessageText()
    {
        wait.Until(_ => Message.Displayed && Message.Text.Length > 0);
        return Message.Text;
    }

    public string MessageKind() => Message.GetDomAttribute("data-kind") ?? string.Empty;

    public bool WorkspaceVisible() => Workspace.Displayed;

    public void FillBooking(string purpose, string station = "PER", double leadMinutes = 120,
        double durationMinutes = 30, string priority = "ROUTINE")
    {
        DateTime starts = DateTime.Now.AddMinutes(leadMinutes);
        DateTime ends = starts.AddMinutes(durationMinutes);

        Station.SelectByValue(station);
        Purpose.Clear();
        Purpose.SendKeys(purpose);
        SetDateTime("starts-at", starts);
        SetDateTime("ends-at", ends);
        Priority.SelectByValue(priority);
    }

    public void SubmitBooking(string purpose, string station = "PER", double leadMinutes = 120,
        double durationMinutes = 30, string priority = "ROUTINE")
    {
        FillBooking(purpose, station, leadMinutes, durationMinutes, priority);
        Submit.Click();
    }

    public IWebElement RowFor(string purpose) =>
        wait.Until(_ => Rows.FirstOrDefault(row => row.Text.Contains(purpose))
            ?? throw new NoSuchElementException($"No booking row containing {purpose}"));

    public string StateOf(string purpose) =>
        RowFor(purpose).FindElement(Test("booking-state")).Text;

    public void Act(string purpose, string action) =>
        RowFor(purpose).FindElement(Test($"action-{action}")).Click();

    public bool HasAction(string purpose, string action) =>
        RowFor(purpose).FindElements(Test($"action-{action}")).Count > 0;

    public void WaitForState(string purpose, string state) =>
        wait.Until(_ => StateOf(purpose) == state);

    private void SetDateTime(string testId, DateTime moment)
    {
        string value = moment.ToString("yyyy-MM-ddTHH:mm");
        IWebElement field = driver.FindElement(Test(testId));
        ((IJavaScriptExecutor)driver).ExecuteScript(
            "arguments[0].value = arguments[1];" +
            "arguments[0].dispatchEvent(new Event('input', { bubbles: true }));" +
            "arguments[0].dispatchEvent(new Event('change', { bubbles: true }));",
            field, value);
    }
}
