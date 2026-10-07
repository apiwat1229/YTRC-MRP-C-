# วิเคราะห์โปรเจกต์ YTRC-ERP ต้นฉบับและแผนย้ายเป็น WPF

## ภาพรวม

โปรเจกต์ต้นฉบับใน `Desktop/YTRC-ERP` เป็น Windows desktop client ที่ใช้ C++20, MFC และ Visual Studio C++ project ไม่ใช่ ERP backend. README ของต้นฉบับระบุชัดว่า shell กับ department navigation มีแล้ว แต่ KPI และรายการงานยังเป็น placeholder; ยังไม่มีการบันทึกหรือส่ง business transactions. FastAPI เป็น service ที่ตั้งใจเชื่อมต่อ แต่สัญญาข้อมูลของแต่ละ workflow ยังไม่ครบ

ด้วยเหตุนี้ผลย้ายครั้งนี้รักษา shell, authentication, shared infrastructure, settings และพื้นที่ทำงานที่เอกสารต้นฉบับรองรับไว้ โดยไม่สร้างข้อมูลธุรกิจสมมติ

## โครงสร้างต้นฉบับ

| ส่วน | ไฟล์หลัก | หน้าที่/สถานะ |
| --- | --- | --- |
| Application | `src/App/YtrcErpApp.*`, `AppConfig.*` | เริ่ม MFC app, โหลดค่าจาก Windows Registry, เตรียม hardware และแสดง sign-in ก่อนเปิด shell |
| Shell | `src/UI/Shell/MainFrame.*`, `ErpRibbonBar.*` | หน้าต่าง SDI, แถบ Home/QA/RRC/Purchase/Shipping/Warehouse/Production/Tools และ command groups |
| Dashboard/view | `src/UI/Views/YtrcErpView.*`, `src/Modules/Dashboard/*` | สลับ workspace และแสดง KPI/department tiles; KPI ใน screenshot เป็น N/A รอ API |
| Department workspaces | `src/Modules/DepartmentWorkspaces.*` | QA, RRC, Purchase, Shipping, Warehouse, Production, Settings และหน้าเฉพาะ user/staff/roles; ส่วนธุรกรรมเป็น starter cards/empty records |
| Auth/RBAC | `src/Core/Auth/*`, `src/Core/Models/{User,Permission,Department}.h`, `src/Modules/Auth/*` | token/session, role/permission checks และ login request ไป FastAPI |
| API | `src/Infrastructure/Api/ApiClient.*` | asynchronous JSON GET/POST/PUT/DELETE และแนบ JWT bearer token |
| Updater | `src/Core/Updater/*` | อ่าน update manifest, เปรียบเทียบ version, ดาวน์โหลด package, launch installer |
| Hardware | `src/Infrastructure/Hardware/*` | สอง COM scales, serial frame parsers, simulation mode, listener callbacks; printer preferences/enumeration |
| Settings workspaces | `src/Modules/Settings/*` | อ่าน users, roles, staff จาก API, filter/export CSV; system dialog เก็บ API/env/update, scales และ printer |
| Resources/docs | `src/Resources/*`, `docs/*` | MFC icon/ribbon assets, runtime screenshot, architecture diagram และ module design |

## หน้าจอและแผนก

Shell ต้นฉบับเป็น ribbon สว่าง: แถบ department อยู่ด้านบน, คำสั่งตาม tab อยู่ใน command band และ workspace อยู่ด้านล่าง. หน้า Home มี refresh, new request, search, reports และ export; ปุ่มใน ribbon เป็น entry points มากกว่าจะเป็น transaction implementation.

Department workspaces ที่มองเห็นจาก shell:

| แผนก | พื้นที่งานในต้นฉบับ | API ที่ README ระบุ |
| --- | --- | --- |
| QA | Lab testing, inspection, non-conformance | `/api/cpk-analyses`, `/api/pools`, `/api/cpk-grades` |
| RRC | Review queue, quality reviews, coordination | `/api/maintenance`, `/api/mymachine`, `/api/it-tickets` ใน API overview |
| Purchase | Requisitions, POs, vendor management | `/api/orders`, `/api/suppliers`, `/api/contracts` |
| Shipping | Shipping plans, bookings, pallet availability | `/api/shipping-plans`, `/api/bookings` |
| Warehouse | Stock control, finished goods, dispatch | `/api/receive-records`, `/api/uss-warehouse`, `/api/pallet-types` |
| Production | Work orders, batch tracking, yield analysis | `/api/production-plans`, `/api/job-orders`, `/api/kanban` |

ในโค้ดยังมีหน้า Raw Material (`/api/receive-records`, `/api/weights`), Maintenance (`/api/maintenance`, `/api/mymachine`) และ IT (`/api/it-tickets`) แต่ไม่ได้เป็น tab แยกใน ribbon ที่ใช้เป็นค่าเริ่มต้น. Settings มี User Accounts, Roles & Permissions, Staff Directory และ Hardware Scales.

## Authentication และสิทธิ์

- Login form ส่ง username/email และ password ไป `POST /api/auth/login`; backend ตอบ `accessToken` หรือ `access_token` พร้อม profile.
- Client เก็บ JWT ไว้ใน session memory และใส่ bearer token ใน API requests. การตั้งค่าจำ username/server ไม่ได้จำ password.
- Role mapping รองรับ System Admin, Manager, Supervisor, Operator. Admin ผ่านทุก department; Home/Tools เปิดให้ทุกคน; primary department, department grants และ raw permission strings ใช้กำหนด department access.
- README เรียก login ว่า fallback demo mode แต่โค้ด `LoginWeb/login.js` ต้องใช้ username/password แล้วส่งไป native host เพื่อเรียก backend. WPF ปัจจุบันให้ลงชื่อเข้าใช้ด้วยบัญชี และอ่าน API URL จาก System Config แทนการแก้ URL บนหน้า Login.

## API และข้อมูลที่มีจริง

- Server เริ่มต้น `https://api.ytrc.co.th`; URL ที่ใส่เพียง host ถูกเติม HTTPS ยกเว้น localhost/127.0.0.1/port 2530 ซึ่งใช้ HTTP.
- Health check ลอง `/api/health/smoke-test` แล้ว fallback `/api/health`.
- User Management เรียก `GET /api/users`; Roles เรียก `GET /api/roles`; Staff Directory ลอง `GET /api/access-control/staff` แล้ว fallback ไป `/api/users` เมื่อได้ 404.
- API overview ใน README ระบุ department routes เพิ่มเติม แต่ source workspaces ยังไม่ดึงหรือบันทึก transaction records เหล่านั้น. Dashboard aggregate endpoint ก็ไม่ได้ระบุ.
- ไม่มี local database, audit implementation หรือ business transaction submit path ในต้นฉบับ.

## Updater และอุปกรณ์

- Update manifest เริ่มต้นจาก `/api/app-updates/ytrc-erp/latest`; parser รองรับชื่อ fields แบบ `versionString/version/latestVersion`, `downloadUrl/url`, `changelog/notes/releaseNotes`, `minVersion` และ SHA-256.
- ต้นฉบับเปรียบเทียบ semantic version, ดาวน์โหลด installer ไป temp folder และสั่งเปิด installer. WPF ทำ manifest check, download และ checksum verification; จะเปิด installer หลังผู้ใช้กดยืนยัน.
- Scale service เดิมรองรับ 2 เครื่อง, COM port, baud/data/parity/stop bits, ทศนิยม, simulation, Tiger TI-01/TWT-01, Toledo และ generic parser. WPF ย้าย settings, COM connection, serial parsing และ simulation.
- Printer settings เดิมเก็บชื่อเครื่องพิมพ์, จำนวนสำเนาและ silent-print preference. WPF ย้ายค่ากำหนดเหล่านี้ แต่การพิมพ์รายงานจริงยังไม่มี workflow ในต้นฉบับ.

## สิ่งที่สร้างใน `YTRC-ERP-C#`

| ความสามารถ | สถานะใน WPF |
| --- | --- |
| WPF app shell, department tabs, command band, YTRC icon, dashboard cards | สร้างแล้ว |
| Login, health check, in-memory token, roles/departments/raw permission checks | สร้างแล้ว |
| Guest preview entry point บนหน้า Login | ถูกเอาออกจาก UI ปัจจุบัน; Login ต้องใช้บัญชี |
| Workflow starter cards ของ 6 tabs และ empty records state | สร้างแล้ว; ไม่แสดงข้อมูลสมมติ |
| System configuration และ local JSON preferences | สร้างแล้ว; เก็บที่ `%LocalAppData%\YTRC-ERP-CSharp\settings.json` |
| Users, roles, staff API read + CSV export | สร้างแล้ว; export ทำงานเมื่อมีข้อมูลโหลดแล้ว |
| Update manifest, download, SHA-256, user-approved launch | สร้างแล้ว |
| Scale serial + simulation, port/model/baud/decimal settings | สร้างแล้ว |
| Printer name/copies/silent preference | ตั้งค่าได้; ยังไม่ได้เชื่อม print workflow |
| Dashboard KPIs, department transactions, new request, report generation, audit history | ยังรอ backend endpoints/data contracts และ business rules |

สถาปัตยกรรม WPF ปัจจุบันจัดเป็น 4 โปรเจกต์ ได้แก่ `YTRC.ERP.Domain`, `YTRC.ERP.Application`, `YTRC.ERP.Infrastructure` และ `YTRC.ERP.Wpf` โดยให้ Domain/Application ไม่อ้างอิง WPF หรือ implementation ของ Infrastructure; WPF เลือก implementation ที่ `App.xaml.cs` ซึ่งเป็น composition root. ภายใน WPF แยกหน้าจอเป็น feature folders, ใช้ Fluent.Ribbon สำหรับแท็บแผนกและคำสั่ง และแยก shell lifecycle/navigation ออกจากกัน. นี่เป็น layered modular monolith ที่กำหนดทิศทาง dependency ชัดเจน แต่หน้าจอบางส่วนยังสร้าง controls และจัดการ events ใน code-behind จึงยังไม่ใช่ MVVM เต็มรูปแบบ. ดูแผนผัง, ขอบเขตแต่ละชั้น และแนวทางเพิ่ม feature ได้ที่ [`ARCHITECTURE.md`](../ARCHITECTURE.md).

## ขั้นต่อไปเมื่อจะพัฒนาเป็น ERP ใช้งานจริง

1. ตกลง response/request schemas และ pagination/filtering สำหรับแผนกกับ dashboard.
2. แยก business workflows, validation และ permission levels ออกจาก UI; เพิ่ม create/update/approve ที่จับคู่กับ backend จริง.
3. กำหนด audit trail, error/retry policy และวิธี refresh/expiry ของ JWT.
4. ยืนยัน update package signing/checksum policy และ version compatibility ก่อนเผยแพร่ installer.
5. เชื่อม printer selection/report templates และทดสอบ COM hardware กับรุ่น/serial protocol ที่ใช้งานในโรงงานจริง.
