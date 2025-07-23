using System;
using System.Diagnostics;
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;

namespace Register
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string profileName = "botprofile";
            string profileDir = @"C:\Temp\FirefoxBotProfile";

            // Create Firefox profile if it doesn't exist
            if (!Directory.Exists(profileDir))
            {
                var process = new Process();
                process.StartInfo.FileName = "C:\\Program Files\\Mozilla Firefox\\firefox.exe";
                process.StartInfo.Arguments = $"-CreateProfile \"{profileName} {profileDir}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
            }

            // Launch Firefox with the specified profile
            FirefoxOptions options = new FirefoxOptions();
            options.AddArgument("-profile");
            options.AddArgument(profileDir);

            using (IWebDriver driver = new FirefoxDriver(options))
            {
                // Navigate to registration page
                driver.Navigate().GoToUrl("https://studentssb9.it.usf.edu/StudentRegistrationSsb/ssb/registration");

                // Wait for and click the <a> element with id "registerLink"
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                var registerLink = wait.Until(drv => drv.FindElement(By.Id("registerLink")));
                registerLink.Click();

                // Wait for possible Microsoft sign-in redirect
                wait.Timeout = TimeSpan.FromMinutes(5); // Increase timeout for user sign-in
                string registrationUrl = "studentssb9.it.usf.edu";
                string microsoftSignInUrlPart = "login.microsoftonline.com";

                try
                {
                    // Wait until redirected to Microsoft sign-in (if applicable)
                    bool wentToMicrosoftSignIn = wait.Until(drv =>
                    {
                        return drv.Url.Contains(microsoftSignInUrlPart) || drv.Url == registrationUrl;
                    });

                    // If redirected to Microsoft sign-in, prompt user to log in and wait until redirected back to registration page
                    if (driver.Url.Contains(microsoftSignInUrlPart))
                    {
                        Console.WriteLine("Please complete the Microsoft sign-in in the browser window. If neccesary.");
                        wait.Until(drv => drv.Url.Contains(registrationUrl));
                    }

                    // Print code continued line
                    Console.WriteLine("Code continued: You are back on the registration page. Check what new buttons need to be clicked.");
                }
                catch (WebDriverTimeoutException)
                {
                    Console.WriteLine("Timed out waiting for sign-in. Press any key to exit.");
                    Console.ReadKey();
                    return;
                }

                // Wait for the browser to finish loading the page
                wait.Timeout = TimeSpan.FromSeconds(30);
                wait.Until(drv =>
                {
                    var js = (IJavaScriptExecutor)drv;
                    return js.ExecuteScript("return document.readyState").ToString() == "complete";
                });

                // Click the <a> tag with class "select2-choice select2-default"
                var termDropdown = driver.FindElement(By.CssSelector("a.select2-choice.select2-default"));
                termDropdown.Click();

                // Wait for the <ul> with id "select2-results-1" to appear
                var resultsList = wait.Until(drv => drv.FindElement(By.Id("select2-results-1")));

                // Find the <div> containing "Fall 2025" inside the results list
                var fall2025Div = resultsList.FindElement(By.XPath(".//div[contains(text(), 'Fall 2025')]"));

                // Get the parent <li> of that <div>
                var fall2025Li = fall2025Div.FindElement(By.XPath("./ancestor::li"));
                // Click the <li> to select "Fall 2025"
                fall2025Li.Click();

                // Get the continue button and click it
                var registerContinue = wait.Until(drv => drv.FindElement(By.Id("term-go")));
                registerContinue.Click();

                // Keep browser open until user presses a key
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
