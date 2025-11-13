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

            // Ask for inputs when not supplied via args
            // args[0] overrides courseCode (e.g., "EML3500")
            // args[1] overrides classChoice (index like "2" or keyword like "Smith" or "LEC")
            string courseCode = null;
            string classChoice = null;

            if (args != null)
            {
                if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) courseCode = args[0].Trim();
                if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])) classChoice = args[1].Trim();
            }

            // Prompt if not provided
            while (string.IsNullOrWhiteSpace(courseCode))
            {
                Console.Write("Enter course code (e.g., ENG3000L): ");
                courseCode = (Console.ReadLine() ?? string.Empty).Trim();
            }

            if (classChoice == null)
            {
                Console.Write("Enter preferred class (index like 2, keyword like Smith/LEC, or leave blank to auto-pick first OPEN): ");
                classChoice = (Console.ReadLine() ?? string.Empty).Trim();
            }

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

                wait.Until(d =>
                {
                    var els = d.FindElements(By.CssSelector("ul.select2-results"));
                    return els.Any(e => e.Displayed);
                });

                var termLi = wait.Until(d =>
                {
                    var div = d.FindElements(By.XPath("//ul[contains(@class,'select2-results')]//div[contains(normalize-space(.), 'Spring 2026')]"))
                               .FirstOrDefault(e => e.Displayed);
                    return div != null ? div.FindElement(By.XPath("./ancestor::li[1]")) : null;
                });
                SafeClick(driver, termLi);

                var registerContinue = wait.Until(d =>
                {
                    var el = d.FindElement(By.Id("term-go"));
                    return (el.Displayed && el.Enabled) ? el : null;
                });
                SafeClick(driver, registerContinue);

                WaitForDocumentReady(driver, wait);

                // Class search Select2
                var inputBox = wait.Until(FindVisibleSelect2Input);
                FocusAndEnsureVisible(driver, inputBox);
                RobustClearSelect2Input(driver, inputBox);
                TypeIntoSelect2(driver, inputBox, courseCode);

                // Select the class token from Select2 dropdown by div id == courseCode
                var classLi = wait.Until(d =>
                {
                    var listVisible = d.FindElements(By.CssSelector("ul.select2-results")).Any(e => e.Displayed);
                    if (!listVisible) return null;

                    var targetDiv = d.FindElements(By.XPath($"//ul[contains(@class,'select2-results')]//div[@id='{courseCode}']"))
                                     .FirstOrDefault(e => e.Displayed);
                    return targetDiv != null ? targetDiv.FindElement(By.XPath("./ancestor::li[1]")) : null;
                });
                SafeClick(driver, classLi);

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

                Thread.Sleep(2000); // brief pause to allow table DOM to stabilize

                // Parse the results table
                var table = driver.FindElement(By.Id("table1"));
                var parsedClasses = ClassTableParser.ParseTable(table);

                // Print a numbered summary to help picking by index
                Console.WriteLine("Search results:");
                for (int i = 0; i < parsedClasses.Count; i++)
                {
                    var c = parsedClasses[i];
                    var status = string.IsNullOrEmpty(c.Status) ? "" : $" [{c.Status}]";
                    Console.WriteLine($"[{i + 1}] {c.Title} - {c.Instructor}{status}");
                }

                if (parsedClasses.Count == 0)
                {
                    Console.WriteLine("No classes found.");
                    return;
                }

                var selectedClass = ChooseClass(parsedClasses, classChoice);

                if (selectedClass == null)
                {
                    Console.WriteLine($"No class matched preference \"{classChoice}\".");
                    return;
                }

                if (IsFull(selectedClass.Status))
                {
                    Console.WriteLine("Selected class is FULL. Starting retry loop (press ESC to cancel).");
                    int attempts = 0;
                    const int delayMs = 5000;

                    while (true)
                    {
                        attempts++;
                        Console.WriteLine($"Attempt {attempts}: refreshing search...");

                        // Click the "Search Again" button
                        var searchAgainBtn = wait.Until(d =>
                        {
                            var el = d.FindElement(By.Id("search-again-button"));
                            return (el.Displayed && el.Enabled) ? el : null;
                        });
                        SafeClick(driver, searchAgainBtn);

                        // Re-run the same search (class box is prefilled)
                        var searchGo = wait.Until(d =>
                        {
                            var el = d.FindElement(By.Id("search-go"));
                            return (el.Displayed && el.Enabled) ? el : null;
                        });
                        SafeClick(driver, searchGo);

                        // Wait for refreshed results: table exists and has at least one visible row
                        wait.Until(d =>
                        {
                            var tableEl = d.FindElements(By.Id("table1")).FirstOrDefault();
                            if (tableEl == null || !tableEl.Displayed) return false;
                            var rows = tableEl.FindElements(By.CssSelector("tbody tr"));
                            return rows.Any(r => r.Displayed);
                        });

                        Thread.Sleep(2000); // brief pause to allow table DOM to stabilize

                        // Parse current table
                        table = driver.FindElement(By.Id("table1"));
                        parsedClasses = ClassTableParser.ParseTable(table);

                        if (parsedClasses.Count == 0)
                        {
                            Console.WriteLine("No classes found after refresh. Retrying...");
                            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                            {
                                Console.WriteLine("Cancelled by user.");
                                break;
                            }
                            Thread.Sleep(delayMs);
                            continue;
                        }

                        selectedClass = ChooseClass(parsedClasses, classChoice);
                        if (selectedClass == null)
                        {
                            Console.WriteLine($"No class matched preference \"{classChoice}\" after refresh. Retrying...");
                            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                            {
                                Console.WriteLine("Cancelled by user.");
                                break;
                            }
                            Thread.Sleep(delayMs);
                            continue;
                        }

                        if (!IsFull(selectedClass.Status))
                        {
                            Console.WriteLine("Class is OPEN! Adding and submitting...");
                            if (selectedClass.AddButton != null)
                            {
                                wait.Until(d => ElementInteractable(selectedClass.AddButton));
                                SafeClick(driver, selectedClass.AddButton);

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
                                Console.WriteLine("Add button not found for the selected class. Will retry.");
                                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                                {
                                    Console.WriteLine("Cancelled by user.");
                                    break;
                                }
                                Thread.Sleep(delayMs);
                                continue;
                            }
                            break; // success
                        }
                        else
                        {
                            Console.WriteLine($"Still FULL at {DateTime.Now:T}.");
                        }

                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                        {
                            Console.WriteLine("Cancelled by user.");
                            break;
                        }
                        Thread.Sleep(delayMs);
                    }
                }
                else
                {
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

        private static ParsedClass ChooseClass(List<ParsedClass> classes, string preference)
        {
            if (classes == null || classes.Count == 0) return null;

            // If numeric, treat as 1-based index
            if (!string.IsNullOrWhiteSpace(preference) && int.TryParse(preference.Trim(), out var idx))
            {
                if (idx >= 1 && idx <= classes.Count) return classes[idx - 1];
            }

            // If keyword provided, try Title/Instructor/Fund contains (case-insensitive)
            if (!string.IsNullOrWhiteSpace(preference))
            {
                var pref = preference.Trim();
                var matches = classes.Where(c =>
                        (!string.IsNullOrEmpty(c.Title) && c.Title.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(c.Instructor) && c.Instructor.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(c.Fund) && c.Fund.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();

                var firstOpen = matches.FirstOrDefault(c => !IsFull(c.Status));
                if (firstOpen != null) return firstOpen;
                if (matches.Count > 0) return matches[0];
            }

            // Default: first not FULL, else first
            var open = classes.FirstOrDefault(c => !IsFull(c.Status));
            return open ?? classes[0];
        }

        private static bool IsFull(string status)
        {
            return !string.IsNullOrWhiteSpace(status) &&
                   status.IndexOf("FULL", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // --- DOM helpers ---

        private static IWebElement FindVisibleSelect2Input(IWebDriver d)
        {
            var inputs = d.FindElements(By.CssSelector("input.select2-input[id^='s2id_autogen']"))
                          .Where(i =>
                          {
                              var cls = i.GetAttribute("class") ?? "";
                              if (cls.Contains("select2-focusser") || cls.Contains("select2-offscreen")) return false;
                              return i.Displayed && i.Enabled;
                          }).ToList();
            if (inputs.Count > 0) return inputs[0];

            var activeDropInput = d.FindElements(By.CssSelector("div.select2-drop-active input.select2-input"))
                                   .FirstOrDefault(i => i.Displayed && i.Enabled);
            return activeDropInput;
        }

        private static void FocusAndEnsureVisible(IWebDriver driver, IWebElement el)
        {
            var js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block:'center'});", el);
            js.ExecuteScript("arguments[0].focus();", el);
        }

        private static void RobustClearSelect2Input(IWebDriver driver, IWebElement el)
        {
            try
            {
                el.Clear();
                if (!string.IsNullOrEmpty(el.GetAttribute("value")))
                {
                    el.SendKeys(Keys.Control + "a");
                    el.SendKeys(Keys.Delete);
                }
                if (!string.IsNullOrEmpty(el.GetAttribute("value")))
                {
                    var js = (IJavaScriptExecutor)driver;
                    js.ExecuteScript("arguments[0].value=''; arguments[0].dispatchEvent(new Event('input',{bubbles:true}));", el);
                }
            }
            catch (ElementNotInteractableException)
            {
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].value=''; arguments[0].dispatchEvent(new Event('input',{bubbles:true}));", el);
            }
        }

        private static void TypeIntoSelect2(IWebDriver driver, IWebElement el, string text)
        {
            try
            {
                el.SendKeys(text);
            }
            catch (ElementNotInteractableException)
            {
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].value=arguments[1]; arguments[0].dispatchEvent(new Event('input',{bubbles:true}));", el, text);
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
            try { return new Uri(url).Host; } catch { return null; }
        }

        private static bool ElementInteractable(IWebElement el)
        {
            try { return el.Displayed && el.Enabled; } catch { return false; }
        }

        private static void SafeClick(IWebDriver driver, IWebElement element)
        {
            try
            {
                element.Click();
            }
            catch
            {
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].click();", element);
            }
        }
    }
}
