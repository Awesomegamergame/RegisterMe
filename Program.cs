using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using RegisterMe;

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
                    Console.WriteLine("You are back on the registration page.");
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

                // Wait 5 seconds before interacting with the input box
                Thread.Sleep(2500);

                // Find the input box by id
                var inputBox = driver.FindElement(By.Id("s2id_autogen5"));

                // Type into the input box
                inputBox.Clear(); // Optional: clear any existing text
                inputBox.SendKeys("EGN3343");

                // Wait a few seconds
                Thread.Sleep(2500);

                // Press Enter
                inputBox.SendKeys(Keys.Enter);

                var searchClass = wait.Until(drv => drv.FindElement(By.Id("search-go")));
                searchClass.Click();

                Thread.Sleep(2500);

                // Wait for the table to be present
                var table = wait.Until(drv => drv.FindElement(By.Id("table1")));

                // Parse and display classes
                var parsedClasses = ClassTableParser.ParseTable(table);
                ClassTableParser.PrintClasses(parsedClasses);

                // Ask user which class to add (for now, default to first)
                int classIndex = 0; // You can prompt the user here if you want
                var selectedClass = parsedClasses[classIndex];

                // Check if the class is FULL
                if (selectedClass.Status != null && selectedClass.Status.ToUpper().Contains("FULL"))
                {
                    Console.WriteLine("Selected class is FULL. Will re-search until available (stub).");
                    // Stub: implement your re-search logic here
                    // Example: while (selectedClass.Status.Contains("FULL")) { ... }
                }
                else
                {
                    // Add the class
                    selectedClass.AddButton?.Click();
                    Console.WriteLine("Class added. Submitting...");

                    // Wait for the submit button to be present and click it
                    var saveButton = wait.Until(drv => drv.FindElement(By.Id("saveButton")));
                    saveButton.Click();

                    Console.WriteLine("Registration submitted.");
                }

                // Keep browser open until user presses a key
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
