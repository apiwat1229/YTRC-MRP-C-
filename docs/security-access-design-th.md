# แนวทางสิทธิ์ YTRC-ERP

## สรุประบบ BE ปัจจุบัน

- BE ใช้ permission code แบบ `module:action` เช่น `bookings:read`, `truckscale:checkin` และตรวจ code แบบตรงตัวที่ route/API
- สิทธิ์ใช้งานมาจากผลรวมของ `Role.permissions` กับ `User.permissions` ซึ่งเป็น direct override ของผู้ใช้
- `get_current_user` อ่านสิทธิ์จากฐานข้อมูลใหม่ในคำขอ API เพื่อให้การเปลี่ยน Role มีผลโดยไม่ต้องออก token ใหม่
- รายการ Role, Permission Catalog, direct user grants และ role health มี API อยู่แล้ว จึงเชื่อมหน้า Settings เข้ากับ BE ได้โดยไม่สร้างแหล่งข้อมูลสิทธิ์ซ้ำ

## ประเด็นที่ควรจัดระเบียบ

1. `seed_roles.py` กำหนด `_ALL_CRUD_PERMISSIONS` ให้ทุก Role ที่ seed รวมถึง Operator, Technician, IT Staff และ Requester ทำให้ Role เหล่านี้เริ่มต้นมีสิทธิ์จัดการผู้ใช้, Role, การอนุมัติ และหลายโมดูลที่ไม่เกี่ยวกับหน้าที่
2. `permissions_required` ให้สิทธิ์ข้ามการตรวจเมื่อ Role มีชื่อ admin ตามรายการที่กำหนดใน BE ควรจำกัด Role นี้ไว้กับผู้ดูแลระบบจำนวนน้อยและใช้ชื่อมาตรฐานเดียวกัน
3. ปิด Permission ใน Catalog ป้องกันการมอบสิทธิ์ใหม่ แต่ไม่ลบสิทธิ์ที่มีอยู่ใน Role หรือ User โดยอัตโนมัติ
4. endpoint ลบ Permission ตรวจว่ามี Role ใช้อยู่หรือไม่ แต่ไม่ได้ตรวจ direct user grants; หน้าจอจึงไม่แสดงคำสั่งลบถาวร และแนะนำให้ปิดใช้งานพร้อมตรวจสิทธิ์ค้าง
5. BE health endpoint ตรวจเฉพาะ grant ของ Role; หน้าจอ YTRC-ERP คำนวณ direct grants ที่ไม่อยู่ใน active catalog เพิ่มจากรายการผู้ใช้

## แนวทาง Role สำหรับ YTRC-ERP

| Role | ขอบเขตที่แนะนำ | หลีกเลี่ยงโดยปริยาย |
| --- | --- | --- |
| Administrator | ดูแลระบบเต็มรูปแบบ; ให้เฉพาะผู้ดูแลที่ได้รับมอบหมาย | แจกจ่ายบัญชีร่วมกัน |
| System Manager | จัดการผู้ใช้, Role, Permission Catalog และ Workflow; ให้สิทธิ์ปฏิบัติการเฉพาะที่จำเป็น | การชั่ง/รับเข้า หรือแก้ข้อมูลปฏิบัติการโดยไม่จำเป็น |
| Supervisor | ดูข้อมูลงานในส่วนรับผิดชอบ, ตรวจอนุมัติ และกำกับทีม | ลบผู้ใช้, แก้ Role, จัดการ Permission Catalog |
| Operator | งาน Booking/คิวและงานรับเข้า/TruckScale ตามหน้าที่; ใช้ update/delete request เมื่อกระบวนการกำหนดให้อนุมัติ | อนุมัติคำขอของตนเอง, ลบข้อมูลจริง, ดูแลบัญชี |
| Technician | อ่าน/ปรับปรุงงานซ่อม เครื่องจักร และอะไหล่ตามหน้าที่ | สิทธิ์ Booking, การอนุมัติ และระบบผู้ใช้ |
| IT Staff | จัดการ IT tickets, assets และบริการที่ดูแล | สิทธิ์งานจัดซื้อ/รับเข้า/ยางโดยไม่จำเป็น |
| Requester | สร้างคำขอและดูเฉพาะรายการของตน หาก endpoint รองรับ ownership filter | อ่าน/แก้/ลบรายการของทุกคน |

ตารางนี้เป็น baseline สำหรับการทบทวน ไม่ได้ถูกนำไปเขียนทับ Role ที่ใช้งานจริงโดยอัตโนมัติ ควรยืนยัน owner ของแต่ละ workflow และทดสอบด้วยบัญชี non-admin ก่อนนำไปใช้กับฐานข้อมูลจริง

## วิธีใช้หน้า Settings ใหม่

- **Overview** — สถิติและเตือน Role ที่มีสิทธิ์ระดับดูแลระบบ
- **Roles & permissions** — สร้าง/แก้/ลบ Role และกำหนด grants ตาม Module/Action; ลบได้เมื่อไม่มีผู้ใช้ผูกอยู่
- **Permission catalog** — สร้างคำจำกัดสิทธิ์และแก้ label/description/สถานะ; module/action ถูกล็อกหลังสร้างเพื่อคง API contract
- **User access** — เปรียบเทียบสิทธิ์จาก Role กับ direct overrides; บันทึกจะแทนที่ direct grants ทั้งชุดของผู้ใช้คนนั้น
- **Access health** — ล้าง Permission ที่ inactive/unknown ออกจาก Role และค้นหา direct grants ที่ต้องทบทวน

การเปลี่ยนสิทธิ์เป็นการเปลี่ยนการเข้าถึง API จริง ไม่ใช่เพียงการซ่อนเมนู ควรให้ Role เป็นสิทธิ์หลัก ใช้ direct override เป็นข้อยกเว้น และทบทวน Role ที่มีสิทธิ์ elevated เป็นระยะ
