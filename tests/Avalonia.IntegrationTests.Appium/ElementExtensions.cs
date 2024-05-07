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
    public record class WindowChrome(
        AppiumElement? Close,
        AppiumElement? Minimize,
        AppiumElement? Maximize,
        AppiumElement? FullScreen);

    internal static class ElementExtensions
    {
        public static IReadOnlyList<AppiumElement> GetChildren(this AppiumElement element) =>
            element.FindElements(MobileBy.XPath("*/*"));

        public static WindowChrome GetChromeButtons(this AppiumElement window)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var closeButton = window.FindElements(MobileBy.AccessibilityId("_XCUI:CloseWindow")).FirstOrDefault();
                var fullscreenButton = window.FindElements(MobileBy.AccessibilityId("_XCUI:FullScreenWindow")).FirstOrDefault();
                var minimizeButton = window.FindElements(MobileBy.AccessibilityId("_XCUI:MinimizeWindow")).FirstOrDefault();
                var zoomButton = window.FindElements(MobileBy.AccessibilityId("_XCUI:ZoomWindow")).FirstOrDefault();
                return new(closeButton, minimizeButton, zoomButton, fullscreenButton);
            }

            throw new NotSupportedException("GetChromeButtons not supported on this platform.");
        }

        public static string GetComboBoxValue(this AppiumElement element)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                element.Text :
                element.GetAttribute("value");
        }
        
        public static string GetName(this AppiumElement element) => GetAttribute(element, "Name", "title");

        public static bool? GetIsChecked(this AppiumElement element)
        {
            var value = GetAttribute(element, "Toggle.ToggleState", "value");
            return value switch
            {
                "0" => false,
                "1" => true,
                "2" => null,
                "On" => true,
                "Off" => false,
                "Indeterminate" => null,
                _ => throw new ArgumentOutOfRangeException($"Unexpected IsChecked value.")
            };
        }

        public static bool GetIsFocused(this AppiumElement element)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var active = element.WrappedDriver.SwitchTo().ActiveElement() as AppiumElement;
                return element.Id == active?.Id;
            }
            else
            {
                // https://stackoverflow.com/questions/71807788/check-if-element-is-focused-in-appium
                throw new NotSupportedException("Couldn't work out how to check if an element is focused on mac.");
            }
        }

        /// <summary>
        /// Clicks a button which is expected to open a new window.
        /// </summary>
        /// <param name="element">The button to click.</param>
        /// <returns>
        /// An object which when disposed will cause the newly opened window to close.
        /// </returns>
        public static IDisposable OpenWindowWithClick(this AppiumElement element)
        {
            var session = element.WrappedDriver;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var oldHandle = session.CurrentWindowHandle;
                var oldHandles = session.WindowHandles.ToList();
                var oldChildWindows = session.FindElements(MobileBy.XPath("//Window"));

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
                    var newChildWindows = session.FindElements(MobileBy.XPath("//Window"));
                    var childWindow = Assert.Single(newChildWindows.Except(oldChildWindows));

                    return Disposable.Create(() =>
                    {
                        new Actions(session)
                            .KeyDown(Keys.Alt)
                            .KeyDown(Keys.F4)
                            .KeyUp(Keys.F4)
                            .KeyUp(Keys.Alt)
                            .Perform();
                    });
                }
            }
            else
            {
                var oldWindows = session.FindElements(MobileBy.XPath("/XCUIElementTypeApplication/XCUIElementTypeWindow"));
                var oldWindowTitles = oldWindows.ToDictionary(x => x.Text);

                element.Click();
                
                // Wait for animations to run.
                Thread.Sleep(1000);

                var newWindows = session.FindElements(MobileBy.XPath("/XCUIElementTypeApplication/XCUIElementTypeWindow"));
                
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
                    var windows = session.FindElements(MobileBy.XPath("/XCUIElementTypeApplication/XCUIElementTypeWindow"));
                    var text = windows.Select(x => x.Text).ToList();
                    var newWindow = session.FindElements(MobileBy.XPath("/XCUIElementTypeApplication/XCUIElementTypeWindow"))
                        .First(x => x.Text == newWindowTitle);
                    var close = ((AppiumElement)newWindow).FindElement(MobileBy.AccessibilityId("_XCUI:CloseWindow"));
                    close!.Click();
                    Thread.Sleep(1000);
                });
            }
        }
    
        public static void SendClick(this AppiumElement element)
        {
            // The Click() method seems to correspond to accessibilityPerformPress on macOS but certain controls
            // such as list items don't support this action, so instead simulate a physical click as VoiceOver
            // does.
            if (OperatingSystem.IsMacOS())
            {
                new Actions(element.WrappedDriver).MoveToElement(element).Click().Perform();
            }
            else
            {
                element.Click();
            }
        }

        public static void MovePointerOver(this AppiumElement element)
        {
            new Actions(element.WrappedDriver).MoveToElement(element).Perform();
        }

        public static string GetAttribute(AppiumElement element, string windows, string macOS)
        {
            return element.GetAttribute(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? windows : macOS);
        }
    }
}
