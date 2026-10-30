# Aurora home: responsive navigation (Development)

The approved dark Aurora landing page, original banner, four priority
cards and all existing diagnostic handlers remain unchanged in function.
This is a layout/navigation improvement only; no J2534 driver, VirtualCar,
ECU profile, PIN, CAN protocol or updater behavior is changed.

## Screen widths

The WinUI 3 home now uses three explicit window-width bands:

| Window width | Primary CEM / ECU / diagnostic cards | Status/quick-action panels |
| --- | --- | --- |
| >= 1280 logical pixels | Four columns, one row | Three columns |
| 900–1279 | Two columns, two rows | Three columns |
| < 900 | One column, four rows | One column, three rows |

The desktop sidebar remains visible; the dashboard scrolls vertically.
There is no forced 1000-pixel dashboard minimum or horizontal scrolling.
The hero keeps the original image and wraps its text on narrow screens.

The previously inert **Главная** sidebar button now returns the landing
dashboard to the banner at the top (without changing the connection).
All other home buttons preserve their original navigation destinations.

Eight structural checks validate breakpoint declarations, 4/2/1 card layout,
narrow status-panel placement, preservation of CEM/ECU navigation, the
functional Home click, absence of horizontal minimum width, and
banner/button preservation. They run against the actual packaged XAML
source without opening WinUI.

**Acceptance limits:** These checks do not replace a user-observed Windows
11 narrow-window screenshot and keyboard/scroll exercise. The CEM PIN
and configuration cards remain explicitly offline-only. No Stable v0.2.4
release files or features are modified.
