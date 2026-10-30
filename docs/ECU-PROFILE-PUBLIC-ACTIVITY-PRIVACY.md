# ECU profile public-activity privacy (Development)

The Dvinholm status bar is mirrored to the Aurora home page's last-activity summary. A locally created ECU profile can have a user-defined name containing a VIN, plate, customer identity, module serial or source notes; selected JSON filenames and folder paths can also contain identifiers. Those details should not be repeated by generic success/error activity.

## Updated behavior

- After creating or editing a profile, global activity confirms it was saved as **unverified** without echoing the profile name.
- Applying a saved profile uses a fixed message and a reminder to check its source and manually confirm the CAN addresses. The actual request/response values still populate the **dedicated, editable diagnostic fields**. The selected profile's name and evidence remain visible in the dedicated profile selector and metadata panel.
- Import and export status retains a **numeric profile count** but omits the original imported filename and export basename. The native file picker remains responsible for choosing and showing the target location.
- A blocked profile export uses a fixed user-facing explanation rather than displaying `InvalidOperationException.Message`, which could contain user-selected paths or other untrusted content.
- This only changes global activity presentation. It does not redact dedicated profile UI fields, the local JSON itself, deliberate user exports, the Aurora selected-profile card, or existing physical CAN/J2534 behavior.

## Eight offline checks

1. Saved profile activity is fixed and payload-free.
2. Applied profile activity avoids CAN addresses and private names.
3. Import reports count without filename.
4. Export reports count without destination basename.
5. Blocked export avoids exception text.
6. Negative counts fail closed.
7. Unknown activity kinds fail closed.
8. Real MainWindow source routes all five outcomes through the safe formatter and retains the dedicated profile metadata and editable CAN ID controls.

No new ECU request, configuration read/write, CEM PIN logic, VirtualCar changes or Stable 0.2.4 modifications. These tests do **not** establish installed Windows GUI acceptance or physical J2534/DiCE compatibility.
