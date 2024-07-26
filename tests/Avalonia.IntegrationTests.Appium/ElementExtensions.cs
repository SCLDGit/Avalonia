using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Interactions;
using Xunit;

namespace Avalonia.IntegrationTests.Appium
{
    public record WindowChrome(
        AppiumWebElement? Close,
        AppiumWebElement? Minimize,
        AppiumWebElement? Maximize,
        AppiumWebElement? FullScreen,
        AppiumWebElement? TitleBar)
    {
        public bool IsAnyButtonEnabled => (TitleBar is null || TitleBar.Enabled) &&
                                          (Close?.Enabled == true
                                           || Minimize?.Enabled == true
                                           || Maximize?.Enabled == true
                                           || FullScreen?.Enabled == true);

        public int TitleBarHeight => TitleBar?.Size.Height ?? -1;

        public int MaxButtonHeight =>
            Math.Max(
                Math.Max(Close?.Size.Height ?? -1, Minimize?.Size.Height ?? -1),
                Math.Max(Maximize?.Size.Height ?? -1, FullScreen?.Size.Height ?? -1));
    }

    internal static class ElementExtensions
    {
        public static IReadOnlyList<AppiumWebElement> GetChildren(this AppiumWebElement element) =>
            element.FindElementsByXPath("*/*");

        public static WindowChrome GetSystemChromeButtons(this AppiumWebElement window)
        {
            if (OperatingSystem.IsMacOS())
            {
                var closeButton = window.FindElementsByAccessibilityId("_XCUI:CloseWindow").FirstOrDefault();
                var fullscreenButton = window.FindElementsByAccessibilityId("_XCUI:FullScreenWindow").FirstOrDefault();
                var minimizeButton = window.FindElementsByAccessibilityId("_XCUI:MinimizeWindow").FirstOrDefault();
                var zoomButton = window.FindElementsByAccessibilityId("_XCUI:ZoomWindow").FirstOrDefault();
                return new(closeButton, minimizeButton, zoomButton, fullscreenButton, null);
            }

            if (OperatingSystem.IsWindows())
            {
                var titlebar = window.FindElementsByTagName("TitleBar").FirstOrDefault();

                // HACK: WinAppDriver doesn't expose AutomationId for title bar buttons for some
                // reason (inspect.exe can see them?!) and the Names are localized so we can't rely on them.
                // We're just going to have to guess which button is which I guess.
                var buttons = titlebar?.FindElementsByXPath(".//Button");
                var closeButton = buttons?.Count > 3 ? buttons[2] : null;
                var minimizeButton = buttons?.Count > 1 ? buttons[0] : null;
                var maximizeButton = buttons?.Count > 2 ? buttons[1] : null;
                return new(closeButton, minimizeButton, maximizeButton, null, titlebar);
            }

            throw new NotSupportedException("GetChromeButtons not supported on this platform.");
        }

        public static WindowChrome GetClientChromeButtons(this AppiumWebElement window)
        {
            var titlebar = window.FindElementsByAccessibilityId("AvaloniaTitleBar")?.FirstOrDefault();
            var closeButton = titlebar?.FindElementByName("Close");
            var minimizeButton = titlebar?.FindElementByName("Minimize");
            var maximizeButton = titlebar?.FindElementByName("Maximize");
            return new(closeButton, minimizeButton, maximizeButton, null, titlebar);
        }

        public static string GetComboBoxValue(this AppiumWebElement element)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                element.Text :
                element.GetAttribute("value");
        }
        
        public static string GetName(this AppiumWebElement element) => GetAttribute(element, "Name", "title");

        public static bool? GetIsChecked(this AppiumWebElement element) =>
            GetAttribute(element, "Toggle.ToggleState", "value") switch
            {
                "0" => false,
                "1" => true,
                "2" => null,
                _ => throw new ArgumentOutOfRangeException($"Unexpected IsChecked value.")
            };

        public static bool GetIsFocused(this AppiumWebElement element)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return element.GetAttribute("HasKeyboardFocus") == "True";
            }
            else
            {
                // https://stackoverflow.com/questions/71807788/check-if-element-is-focused-in-appium
                throw new NotSupportedException("Couldn't work out how to check if an element is focused on mac.");
            }
        }

        public static AppiumWebElement GetCurrentSingleWindow(this AppiumDriver session)
        {
            if (OperatingSystem.IsMacOS())
            {
                // The Avalonia a11y tree currently exposes two nested Window elements, this is a bug and should be fixed 
                // but in the meantime use the `parent::' selector to return the parent "real" window. 
                return session.FindElementByXPath(
                    $"XCUIElementTypeWindow//*/parent::XCUIElementTypeWindow");
            }
            else
            {
                return session.FindElementByXPath($"//Window");
            }
        }

        public static AppiumWebElement GetWindowById(this AppiumDriver session, string identifier)
        {
            if (OperatingSystem.IsMacOS())
            {
                return session.FindElementByXPath(
                    $"XCUIElementTypeWindow[@identifier='{identifier}']");
            }
            else
            {
                return session.FindElementByXPath($"//Window[@AutomationId='{identifier}']");
            }
        }


        /// <summary>
        /// Clicks a button which is expected to open a new window.
        /// </summary>
        /// <param name="element">The button to click.</param>
        /// <returns>
        /// An object which when disposed will cause the newly opened window to close.
        /// </returns>
        public static IDisposable OpenWindowWithClick(this AppiumWebElement element)
        {
            var session = element.WrappedDriver;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var oldHandle = session.CurrentWindowHandle;
                var oldHandles = session.WindowHandles.ToList();
                var oldChildWindows = session.FindElements(By.XPath("//Window"));

                element.Click();

                var newHandle = session.WindowHandles.Except(oldHandles).SingleOrDefault();

                if (newHandle is not null)
                {
                    // A new top-level window was opened. We need to switch to it.
                    session.SwitchTo().Window(newHandle);

                    return Disposable.Create(() =>
                    {
                        session.Close();
                        session.SwitchTo().Window(oldHandle);
                    });
                }
                else
                {
                    // If a new window handle hasn't been added to the session then it's likely
                    // that a child window was opened. These don't appear in session.WindowHandles
                    // so we have to use an XPath query to get hold of it.
                    var newChildWindows = session.FindElements(By.XPath("//Window"));
                    var childWindow = Assert.Single(newChildWindows.Except(oldChildWindows));

                    return Disposable.Create(() =>
                    {
                        childWindow.SendKeys(Keys.Alt + Keys.F4 + Keys.Alt);
                    });
                }
            }
            else
            {
                var oldWindows = session.FindElements(By.XPath("/XCUIElementTypeApplication/XCUIElementTypeWindow"));
                var oldWindowTitles = oldWindows.ToDictionary(x => x.Text);

                element.Click();
                
                // Wait for animations to run.
                Thread.Sleep(1000);

                var newWindows = session.FindElements(By.XPath("/XCUIElementTypeApplication/XCUIElementTypeWindow"));
                
                // Try to find the new window by looking for a window with a title that didn't exist before the button
                // was clicked. Sometimes it seems that when a window becomes fullscreen, all other windows in the
                // application lose their titles, so filter out windows with no title (this may have started happening
                // with macOS 13.1?)
                var newWindowTitles = newWindows
                    .Select(x => (x.Text, x))
                    .Where(x => !string.IsNullOrEmpty(x.Text))
                    .ToDictionary(x => x.Text, x => x.x);

                var newWindowTitle = Assert.Single(newWindowTitles.Keys.Except(oldWindowTitles.Keys));

                return Disposable.Create(() =>
                {
                    // TODO: We should be able to use Cmd+W here but Avalonia apps don't seem to have this shortcut
                    // set up by default.
                    var windows = session.FindElements(By.XPath("/XCUIElementTypeApplication/XCUIElementTypeWindow"));
                    var text = windows.Select(x => x.Text).ToList();
                    var newWindow = session.FindElements(By.XPath("/XCUIElementTypeApplication/XCUIElementTypeWindow"))
                        .First(x => x.Text == newWindowTitle);
                    var close = ((AppiumWebElement)newWindow).FindElementByAccessibilityId("_XCUI:CloseWindow");
                    close!.Click();
                    Thread.Sleep(1000);
                });
            }
        }
    
        public static void SendClick(this AppiumWebElement element)
        {
            // The Click() method seems to correspond to accessibilityPerformPress on macOS but certain controls
            // such as list items don't support this action, so instead simulate a physical click as VoiceOver
            // does. On Windows, Click() seems to fail with the WindowState checkbox for some reason.
            new Actions(element.WrappedDriver).MoveToElement(element).Click().Perform();
        }

        public static void MovePointerOver(this AppiumWebElement element)
        {
            new Actions(element.WrappedDriver).MoveToElement(element).Perform();
        }

        public static string GetAttribute(AppiumWebElement element, string windows, string macOS)
        {
            return element.GetAttribute(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? windows : macOS);
        }
    }
}
