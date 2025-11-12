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
            var options = new FirefoxOptions();
            options.AddArgument("-profile");
            options.AddArgument(profileDir);

            using (IWebDriver driver = new FirefoxDriver(options))
            {
                // Use only explicit waits (no implicit wait to avoid interference)
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);

                var wait = CreateWait(driver, TimeSpan.FromSeconds(30));
                var longWait = CreateWait(driver, TimeSpan.FromMinutes(5));

                // Navigate to registration page
                driver.Navigate().GoToUrl("https://studentssb9.it.usf.edu/StudentRegistrationSsb/ssb/registration");

                // Wait for and click the <a> element with id "registerLink"
                var registerLink = wait.Until(d =>
                {
                    var el = d.FindElement(By.Id("registerLink"));
                    return (el.Displayed && el.Enabled) ? el : null;
                });
                SafeClick(driver, registerLink);

                // Handle possible Microsoft sign-in redirect with fast bounce
                string registrationHost = "studentssb9.it.usf.edu";
                string microsoftSignInHost = "login.microsoftonline.com";

                // Wait until we land on either Microsoft sign-in or stay/get back to the registration host
                string landedHost = wait.Until(d =>
                {
                    string host = TryGetHost(d.Url);
                    if (host == null) return null;
                    if (host.Contains(microsoftSignInHost) || host.Contains(registrationHost))
                        return host;
                    return null;
                });

                // If redirected to Microsoft sign-in, prompt user and wait until redirected back
                if (landedHost.Contains(microsoftSignInHost))
                {
                    Console.WriteLine("Please complete the Microsoft sign-in in the browser window (if necessary). Waiting to return to registration...");
                    bool backToRegistration = longWait.Until(d =>
                    {
                        string host = TryGetHost(d.Url);
                        return host != null && host.Contains(registrationHost);
                    });
                    if (!backToRegistration)
                    {
                        Console.WriteLine("Timed out waiting to return from Microsoft sign-in. Press any key to exit.");
                        Console.ReadKey();
                        return;
                    }
                }

                // Ensure DOM is ready (and jQuery idle if present)
                WaitForDocumentReady(driver, wait);

                // Open the term Select2 dropdown
                var termDropdown = wait.Until(d =>
                {
                    var el = d.FindElement(By.CssSelector("a.select2-choice.select2-default, a.select2-choice"));
                    return el.Displayed && el.Enabled ? el : null;
                });
                SafeClick(driver, termDropdown);

                // Wait for results container to appear (Select2)
                var resultsContainer = wait.Until(d =>
                {
                    // Select2 results are usually an <ul> with class containing "select2-results"
                    var els = d.FindElements(By.CssSelector("ul.select2-results"));
                    return els.FirstOrDefault(e => e.Displayed);
                });

                // Wait for and click the option containing "Spring 2026"
                var termOption = wait.Until(d =>
                {
                    // Match any div inside results containing the text
                    var candidates = d.FindElements(By.XPath("//ul[contains(@class,'select2-results')]//div[contains(normalize-space(.), 'Spring 2026')]"));
                    var el = candidates.FirstOrDefault(e => e.Displayed);
                    return el;
                });
                // Click the parent <li> of that <div>, which is the actual selectable item
                var termLi = termOption.FindElement(By.XPath("./ancestor::li[1]"));
                SafeClick(driver, termLi);

                // Click Continue
                var registerContinue = wait.Until(d =>
                {
                    var el = d.FindElement(By.Id("term-go"));
                    return (el.Displayed && el.Enabled) ? el : null;
                });
                SafeClick(driver, registerContinue);

                // Wait until the page settles after term selection
                WaitForDocumentReady(driver, wait);

                // Find the search input (Select2 autogenerated id can vary; use starts-with)
                var inputBox = wait.Until(d =>
                {
                    var inputs = d.FindElements(By.XPath("//input[starts-with(@id,'s2id_autogen')]"));
                    var el = inputs.FirstOrDefault(e => e.Displayed && e.Enabled);
                    return el;
                });

                inputBox.Clear();
                inputBox.SendKeys("EML3500");

                // Wait for Select2 to populate results (optional but robust)
                wait.Until(d =>
                {
                    var items = d.FindElements(By.CssSelector("ul.select2-results li"));
                    return items.Any(i => i.Displayed);
                });

                inputBox.SendKeys(Keys.Enter);

                // Click search
                var searchClass = wait.Until(d =>
                {
                    var el = d.FindElement(By.Id("search-go"));
                    return (el.Displayed && el.Enabled) ? el : null;
                });
                SafeClick(driver, searchClass);

                // Wait for the results table to be present and have at least one visible row
                wait.Until(d =>
                {
                    var tableEl = d.FindElements(By.Id("table1")).FirstOrDefault();
                    if (tableEl == null || !tableEl.Displayed) return false;
                    var rows = tableEl.FindElements(By.CssSelector("tbody tr"));
                    return rows.Any(r => r.Displayed);
                });

                var table = driver.FindElement(By.Id("table1"));

                // Parse and display classes
                var parsedClasses = ClassTableParser.ParseTable(table);
                ClassTableParser.PrintClasses(parsedClasses);

                // Choose the first class (index 0), with bounds check
                int classIndex = 0;
                if (parsedClasses.Count == 0)
                {
                    Console.WriteLine("No classes found. Exiting.");
                    return;
                }
                var selectedClass = parsedClasses[classIndex];

                // Check if the class is FULL
                if (!string.IsNullOrWhiteSpace(selectedClass.Status) &&
                    selectedClass.Status.IndexOf("FULL", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("Selected class is FULL. Will re-search until available (stub).");
                    // TODO: Add a retry loop that refreshes the search and re-parses until status changes.
                }
                else
                {
                    // Wait for Add button to be clickable and click it
                    if (selectedClass.AddButton != null)
                    {
                        wait.Until(d => ElementInteractable(selectedClass.AddButton));
                        SafeClick(driver, selectedClass.AddButton);
                        Console.WriteLine("Class added. Submitting...");

                        // Wait for the submit button to be present and click it
                        var saveButton = wait.Until(d =>
                        {
                            var el = d.FindElement(By.Id("saveButton"));
                            return (el.Displayed && el.Enabled) ? el : null;
                        });
                        SafeClick(driver, saveButton);

                        Console.WriteLine("Registration submitted.");
                    }
                    else
                    {
                        Console.WriteLine("Add button not found for the selected class.");
                    }
                }

                // Keep browser open until user presses a key
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        private static WebDriverWait CreateWait(IWebDriver driver, TimeSpan timeout)
        {
            var wait = new WebDriverWait(driver, timeout)
            {
                PollingInterval = TimeSpan.FromMilliseconds(250)
            };
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
            return wait;
        }

        private static void WaitForDocumentReady(IWebDriver driver, WebDriverWait wait)
        {
            wait.Until(d =>
            {
                try
                {
                    var js = (IJavaScriptExecutor)d;
                    var ready = (string)js.ExecuteScript("return document.readyState");
                    if (ready != "complete") return false;

                    // If jQuery is present, wait for it to be idle
                    var hasJq = (bool)js.ExecuteScript("return !!window.jQuery");
                    if (hasJq)
                    {
                        var active = (long)js.ExecuteScript("return window.jQuery.active");
                        return active == 0;
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        private static string TryGetHost(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return null;
            }
        }

        private static bool ElementInteractable(IWebElement el)
        {
            try
            {
                return el.Displayed && el.Enabled;
            }
            catch
            {
                return false;
            }
        }

        private static void SafeClick(IWebDriver driver, IWebElement element)
        {
            try
            {
                element.Click();
            }
            catch (Exception)
            {
                try
                {
                    var js = (IJavaScriptExecutor)driver;
                    js.ExecuteScript("arguments[0].click();", element);
                }
                catch
                {
                    // Swallow and let upstream waits handle failures if any
                    throw;
                }
            }
        }
    }
}
