using System;
using System.Diagnostics;
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

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
                driver.Navigate().GoToUrl("https://studentssb9.it.usf.edu/StudentRegistrationSsb/ssb/registration/registration");

                // Wait for and click the "Register for Classes" button
                var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(driver, TimeSpan.FromSeconds(10));
                var registerButton = wait.Until(drv => drv.FindElement(By.XPath("//button[contains(., 'Register for Classes')]")));
                registerButton.Click();

                // Keep browser open until user presses a key
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
