# Purchasing → SI Export

Create schedules directly in the app with **New SI Export**, then add contracts
using the line editor. Its tabs cover commercial/quantity fields, routing/carrier
fields and planned/actual milestone dates. Draft/Forecast allows incomplete
logistics. Master suggestions remain editable for new ports/carriers.

Use **แก้ไข / Tracking** to change a schedule; each save requires a revision
reason. Use **ยืนยันฉบับใช้งาน** after resolving the displayed missing SI fields.
Commercial changes require confirmation again. Actual milestone updates retain
the commercial confirmation. **Revision / Compare** shows prior snapshots and
field changes. Copies receive new line IDs and clear actual milestones.

The list supports search, year/month filters and pagination. **Excel แยกสายเรือ**
exports the current schedule for its recipient into sheets grouped by carrier.
All units and planned/actual dates remain explicit. History also exports a chosen
revision. **รายงาน / พิมพ์** opens the existing A4 print preview; use a PDF printer
to save a PDF. Cancelling a schedule preserves its audit history.

The new Application DTOs and validation rules are under
`Features/SIExport`, typed HTTP mapping is in Infrastructure `SIExportService`,
and WPF screens are under `Features/SIExport`. The app's composition root injects
`ISIExportService`. Access uses the existing Purchasing `orders:*` permissions.

Historical data includes 70 schedules and 917 current lines from January 2025 through October 2026. Review flags block confirmation until checked. Source and inherited-field provenance remain available. New records are created directly in the app.

