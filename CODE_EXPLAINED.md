# StudentAPI — คู่มืออธิบายโค้ดละเอียด (เตรียมสัมภาษณ์)

> โปรเจกต์ ASP.NET Core Web API ทำ CRUD ข้อมูลนักศึกษา (Student)
> เทคโนโลยี: **.NET 10 · EF Core 10 · PostgreSQL · Docker · Swagger/OpenAPI**
> สถาปัตยกรรมแบบแบ่งชั้น (Layered): **Controller → Service → DbContext → Database**

---

## สารบัญ
1. [ภาพรวมสถาปัตยกรรม](#1-ภาพรวมสถาปัตยกรรม)
2. [ไล่โค้ดทีละไฟล์](#2-ไล่โค้ดทีละไฟล์)
3. [Data Flow: ตาม 1 request ตั้งแต่ต้นจนจบ](#3-data-flow-ตาม-1-request-ตั้งแต่ต้นจนจบ)
4. [แนวคิด/เทคนิคสำคัญที่ต้องอธิบายให้ได้](#4-แนวคิดเทคนิคสำคัญที่ต้องอธิบายให้ได้)
5. [คำถามสัมภาษณ์ที่น่าจะโดน + คำตอบ](#5-คำถามสัมภาษณ์ที่น่าจะโดน--คำตอบ)
6. [จุดที่ปรับปรุงได้ (ถ้าเขาถามว่าจะทำให้ดีขึ้นยังไง)](#6-จุดที่ปรับปรุงได้)

---

## 1. ภาพรวมสถาปัตยกรรม

```
HTTP Request
    │
    ▼
┌─────────────────────┐
│ StudentsController  │  ← รับ request, แปลงเป็น HTTP response (ชั้นบางๆ)
│  (Presentation)     │     ไม่มี business logic
└─────────┬───────────┘
          │ เรียกผ่าน interface IStudentService
          ▼
┌─────────────────────┐
│  StudentService     │  ← business logic ทั้งหมด (เช่น "StudentNo ห้ามซ้ำ", trim, ค้นหา)
│  (Business Logic)   │     คืนผลเป็น StudentServiceResult (ไม่ผูกกับ HTTP)
└─────────┬───────────┘
          │ ใช้ EF Core
          ▼
┌─────────────────────┐
│   AppDbContext      │  ← ตัวแทนฐานข้อมูลในโค้ด (DbSet<Student> = ตาราง)
│  (Data Access)      │
└─────────┬───────────┘
          │ Npgsql provider แปลง LINQ → SQL
          ▼
    PostgreSQL (studentdb)
```

**ทำไมต้องแบ่งชั้น?** — แต่ละชั้นมีหน้าที่เดียว (Separation of Concerns):
- Controller เปลี่ยนได้โดยไม่แตะ logic (เช่นเปลี่ยนจาก REST เป็น gRPC)
- Service เอาไป unit test ได้โดยไม่ต้องมี HTTP
- ถ้าเปลี่ยน DB จาก PostgreSQL เป็นอื่น แก้แค่ชั้น Data

---

## 2. ไล่โค้ดทีละไฟล์

### 2.1 `Program.cs` — จุดเริ่มต้นและการตั้งค่า (Composition Root)

```csharp
var builder = WebApplication.CreateBuilder(args);

// ลงทะเบียน AppDbContext + ใช้ PostgreSQL ด้วย connection string จาก appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ลงทะเบียน Student Service (ชั้น business logic)
builder.Services.AddScoped<IStudentService, StudentService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

**อธิบาย:**
- ใช้รูปแบบ **Minimal Hosting** ของ .NET (ไม่มีคลาส `Startup` แยกแล้ว — รวมอยู่ใน `Program.cs`)
- **Dependency Injection (DI)** คือหัวใจ — เราไม่ `new` object เอง แต่ "ลงทะเบียน" ไว้กับ container แล้วมันฉีดให้เอง
  - `AddDbContext<AppDbContext>` → ลงทะเบียน DbContext แบบ **Scoped** โดยอัตโนมัติ (1 instance ต่อ 1 request)
  - `AddScoped<IStudentService, StudentService>` → เวลาใครขอ `IStudentService` ให้สร้าง `StudentService` ให้
- **Middleware Pipeline** (ลำดับสำคัญมาก — ทำงานจากบนลงล่าง):
  1. `UseHttpsRedirection` — บังคับ HTTPS
  2. `UseAuthorization` — ตรวจสิทธิ์ (ตอนนี้ยังไม่มี auth จริง แต่วาง pipeline ไว้)
  3. `MapControllers` — ส่ง request ไปหา controller ที่ตรง route
- Swagger/OpenAPI เปิดเฉพาะตอน **Development** (เพื่อความปลอดภัย ไม่เปิดบน production)

> 💡 **สัมภาษณ์**: "ลำดับ middleware สำคัญไหม?" → สำคัญมาก เช่น `UseAuthorization` ต้องอยู่หลัง `UseAuthentication` (ถ้ามี) และก่อน `MapControllers` ไม่งั้นสิทธิ์จะไม่ถูกเช็ค

---

### 2.2 `Models/Student.cs` — โมเดลข้อมูล (Entity)

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StudentStatus
{
    Active,
    Inactive
}

public class Student
{
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Student No ห้ามว่าง")]
    public string StudentNo { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Student Name ห้ามว่าง")]
    public string StudentName { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Level ห้ามว่าง")]
    public string Level { get; set; } = string.Empty;

    public StudentStatus Status { get; set; } = StudentStatus.Active;
}
```

**อธิบาย:**
- คลาสนี้เป็นได้ทั้ง **Entity** (ตาราง DB) และ **DTO** (รูปแบบ JSON ที่รับ/ส่ง) — ในโปรเจกต์เล็กใช้ตัวเดียวกันได้ (ดู [ข้อจำกัด](#6-จุดที่ปรับปรุงได้))
- **Data Annotations** (`[Required]`) = validation ระดับ model
  - `AllowEmptyStrings = false` → ส่ง `""` มาก็ไม่ผ่าน
  - ASP.NET Core เช็คให้อัตโนมัติเพราะมี `[ApiController]` ที่ controller (ดูข้อ 2.3)
- `= string.Empty` → กัน null warning (เพราะเปิด `<Nullable>enable</Nullable>`)
- **`[JsonStringEnumConverter]`** → ส่ง/รับ enum เป็นข้อความ `"Active"` แทนตัวเลข `0` ใน JSON (อ่านง่าย, ไม่พังถ้าสลับลำดับ enum)

> 💡 **สัมภาษณ์**: "ทำไม default `Status = Active`?" → ให้ค่า sensible default ตอน create โดยไม่ต้องส่งมา

---

### 2.3 `Controllers/StudentsController.cs` — ชั้นรับ HTTP

```csharp
[ApiController]
[Route("[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _service;

    public StudentsController(IStudentService service)  // ← DI ฉีด service เข้ามา
    {
        _service = service;
    }
    ...
}
```

**Attribute สำคัญ:**
- **`[ApiController]`** — เปิดพฤติกรรมพิเศษ:
  - **Automatic model validation** — ถ้า `ModelState` ไม่ผ่าน (เช่น `StudentNo` ว่าง) จะคืน `400 Bad Request` ให้อัตโนมัติ **ก่อน** เข้า method (เราไม่ต้องเขียน `if (!ModelState.IsValid)` เอง)
  - Bind `[FromBody]` ให้อัตโนมัติ
- **`[Route("[controller]")]`** — `[controller]` = ชื่อคลาสตัดคำว่า "Controller" ออก → route คือ `/Students`
- สืบทอด `ControllerBase` (ไม่ใช่ `Controller`) เพราะเป็น API ล้วน ไม่ต้อง render View

**แต่ละ endpoint:**

| Method | Route | หน้าที่ | Response สำเร็จ |
|--------|-------|---------|------------------|
| `GetAll` | `GET /Students?studentNo=&studentName=` | ดึงทั้งหมด + ค้นหา | `200 OK` + list |
| `GetById` | `GET /Students/{id}` | ดึงตาม id | `200 OK` หรือ `404` |
| `Create` | `POST /Students` | เพิ่มใหม่ | `201 Created` |
| `Update` | `PUT /Students/{id}` | แก้ทั้งตัว | `200 OK` |
| `Delete` | `DELETE /Students/{id}` | ลบ | `204 No Content` |

**ตัวอย่างที่ต้องอธิบายให้ได้ — `Create`:**

```csharp
[HttpPost]
public async Task<ActionResult<Student>> Create(Student student)
{
    var result = await _service.CreateAsync(student);
    if (result.Status == StudentResultStatus.Duplicate)
    {
        ModelState.AddModelError(nameof(Student.StudentNo), result.Error!);
        return ValidationProblem(ModelState);   // → 400 + รูปแบบ error มาตรฐาน
    }

    // 201 Created + header Location ชี้ไป GET /Students/{id}
    return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
}
```

**ประเด็นที่กรรมการชอบถาม:**
- **`CreatedAtAction`** → คืน `201 Created` พร้อม HTTP header `Location: /Students/5` (ตามหลัก REST: สร้างเสร็จต้องบอกว่าไปดูของใหม่ได้ที่ไหน)
- **`ValidationProblem`** → คืน error แบบมาตรฐาน **RFC 7807 (ProblemDetails)** — frontend อ่านง่าย ฟอร์แมตเดียวกันทั้งระบบ
- Controller **ไม่มี business logic เลย** — แค่แปลผลลัพธ์ (`result.Status`) เป็น HTTP status code ที่ถูกต้อง
- `[HttpGet("{id:int}")]` — `:int` คือ **route constraint** บังคับว่า id ต้องเป็นตัวเลข (ส่ง `/Students/abc` จะไม่ match เลย → 404)

**`GetById`** — แปลง status ของ service เป็น HTTP:
```csharp
var result = await _service.GetByIdAsync(id);
if (result.Status == StudentResultStatus.NotFound)
    return NotFound(new { message = result.Error });  // 404
return Ok(result.Value);                               // 200
```

---

### 2.4 `Services/IStudentService.cs` + `StudentService.cs` — ชั้น Business Logic

**Interface** (สัญญาว่ามีเมธอดอะไรบ้าง):
```csharp
public interface IStudentService
{
    Task<IEnumerable<Student>> GetAllAsync(string? studentNo, string? studentName);
    Task<StudentServiceResult<Student>> GetByIdAsync(int id);
    Task<StudentServiceResult<Student>> CreateAsync(Student student);
    Task<StudentServiceResult<Student>> UpdateAsync(int id, Student input);
    Task<StudentServiceResult<Student>> DeleteAsync(int id);
}
```

> **ทำไมต้องมี interface?** — Controller พึ่งพา **abstraction** ไม่ใช่ concrete class (หลักการ **Dependency Inversion** ตัว D ใน SOLID) → สลับ implementation หรือ mock ตอนเทสได้ง่าย

**Implementation — จุดที่ต้องเข้าใจลึก:**

#### GetAllAsync — ค้นหาแบบ dynamic query
```csharp
public async Task<IEnumerable<Student>> GetAllAsync(string? studentNo, string? studentName)
{
    var query = _db.Students.AsNoTracking();   // ← สำคัญ

    if (!string.IsNullOrWhiteSpace(studentNo))
        query = query.Where(s => EF.Functions.ILike(s.StudentNo, $"%{studentNo.Trim()}%"));

    if (!string.IsNullOrWhiteSpace(studentName))
        query = query.Where(s => EF.Functions.ILike(s.StudentName, $"%{studentName.Trim()}%"));

    return await query.OrderBy(s => s.Id).ToListAsync();
}
```
- **`AsNoTracking()`** — บอก EF ว่าแค่อ่าน ไม่แก้ → ไม่ต้องเก็บ change tracking → **เร็วขึ้น + กิน memory น้อยลง** (best practice สำหรับ query อ่านอย่างเดียว)
- **Deferred Execution** — `query` ยังไม่ยิง SQL จนกว่าจะ `ToListAsync()` → เราต่อ `.Where()` เพิ่มเงื่อนไขได้เรื่อยๆ แล้วค่อยยิงทีเดียว (สร้าง WHERE แบบ dynamic)
- **`EF.Functions.ILike`** — ฟังก์ชันเฉพาะ PostgreSQL: ค้นแบบ **case-insensitive** (`ILIKE`) + `%...%` = ค้นบางส่วน (partial match)
- ทั้งหมดนี้แปลเป็น SQL แล้วรันที่ DB (ไม่ได้ดึงมา filter ในหน่วยความจำ)

#### CreateAsync — เช็คซ้ำ + business rule
```csharp
public async Task<StudentServiceResult<Student>> CreateAsync(Student student)
{
    student.StudentNo = student.StudentNo.Trim();      // normalize ข้อมูลก่อน
    student.StudentName = student.StudentName.Trim();
    student.Level = student.Level.Trim();

    var exists = await _db.Students
        .AnyAsync(s => s.StudentNo.ToLower() == student.StudentNo.ToLower());
    if (exists)
        return StudentServiceResult<Student>.Duplicate($"Student No '{student.StudentNo}' นี้มีอยู่แล้ว (ห้ามซ้ำ)");

    student.Id = 0;                    // บังคับให้ DB gen id เอง (กันคนส่ง id มาเอง)
    _db.Students.Add(student);         // mark ว่าจะ insert
    await _db.SaveChangesAsync();      // ยิง INSERT จริงตรงนี้
    return StudentServiceResult<Student>.Success(student);
}
```
- **Trim ก่อนเสมอ** — กันช่องว่างหน้า/หลังทำให้ข้อมูลเพี้ยน/ซ้ำแบบมองไม่เห็น
- **เช็คซ้ำด้วย `AnyAsync`** — เร็วกว่าดึงทั้ง object มา (แปลเป็น `SELECT EXISTS(...)`)
- **`SaveChangesAsync`** = จุดที่ commit ลง DB จริง (ก่อนหน้านั้นแค่ track ใน memory)

#### UpdateAsync — เช็คซ้ำ "กับคนอื่น"
```csharp
var duplicated = await _db.Students
    .AnyAsync(s => s.Id != id && s.StudentNo.ToLower() == newStudentNo.ToLower());
```
- จุดต่างสำคัญ: `s.Id != id` → ยอมให้ StudentNo ซ้ำกับ **ตัวเอง** ได้ (เพราะกำลังแก้ record เดิม) แต่ห้ามซ้ำกับ record อื่น
- โหลด entity เดิมด้วย `FindAsync` แล้วแก้ property → EF **detect การเปลี่ยนแปลงเอง** (change tracking) → `SaveChangesAsync` ยิง `UPDATE` เฉพาะคอลัมน์ที่เปลี่ยน

> 💡 นี่คือ pattern **"load then modify"** — ต่างจาก create ที่ `Add` เข้าไปใหม่

---

### 2.5 `Services/StudentServiceResult.cs` — Result Pattern

```csharp
public enum StudentResultStatus { Success, NotFound, Duplicate }

public class StudentServiceResult<T>
{
    public StudentResultStatus Status { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => Status == StudentResultStatus.Success;

    public static StudentServiceResult<T> Success(T value) => new() { Status = ..., Value = value };
    public static StudentServiceResult<T> NotFound(string error) => new() { Status = ..., Error = error };
    public static StudentServiceResult<T> Duplicate(string error) => new() { Status = ..., Error = error };
}
```

**นี่คือจุดขายของโปรเจกต์ — ต้องอธิบายให้ได้:**
- เรียกว่า **Result Pattern** (หรือ Operation Result)
- **ปัญหาที่แก้:** ถ้า service คืนแค่ `Student` มันบอกไม่ได้ว่า "ไม่พบ" กับ "ซ้ำ" ต่างกันยังไง จะโยน exception ก็เปลืองและช้า
- **แนวคิด:** service คืน object ที่บอก 3 อย่าง: เกิดอะไรขึ้น (`Status`) / ได้ค่าอะไร (`Value`) / error ข้อความว่าอะไร (`Error`)
- **ข้อดีหลัก:** ชั้น Service **ไม่ผูกกับ HTTP** — มันไม่รู้จัก 404/400 ด้วยซ้ำ มันรู้แค่ `NotFound`/`Duplicate` แล้วให้ Controller เป็นคนแปลเป็น HTTP status → เอา service ไปใช้ที่อื่น (console app, gRPC) ได้
- `init` = ตั้งค่าได้ตอนสร้างเท่านั้น แก้ทีหลังไม่ได้ (immutable → ปลอดภัย)
- **static factory methods** (`Success`, `NotFound`, `Duplicate`) → สร้าง object อ่านง่าย ชัดเจนกว่า `new`

> 💡 **สัมภาษณ์**: "ทำไมไม่ throw exception?" → Exception ควรใช้กับกรณี "ผิดปกติจริงๆ" ไม่ใช่ flow ปกติของธุรกิจ (เช่น "ไม่พบข้อมูล" เป็นเรื่องปกติ) + exception มี performance cost + result pattern บังคับให้ caller จัดการทุกกรณีอย่างชัดเจน

---

### 2.6 `Data/AppDbContext.cs` — ชั้น Data Access (EF Core)

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();   // 1 DbSet = 1 ตาราง

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasIndex(s => s.StudentNo).IsUnique();        // ★ unique ที่ระดับ DB
            entity.Property(s => s.StudentNo).HasMaxLength(20);
            entity.Property(s => s.StudentName).HasMaxLength(100);
            entity.Property(s => s.Level).HasMaxLength(50);
            entity.Property(s => s.Status)
                  .HasConversion<string>()                       // enum → เก็บเป็น text ใน DB
                  .HasMaxLength(20);
        });
    }
}
```

**อธิบาย:**
- `DbContext` = **Unit of Work** — คุมการ track การเปลี่ยนแปลงและ commit ทีเดียวด้วย `SaveChanges`
- `DbSet<Student>` = **Repository** ของตาราง Students (query/add/remove ผ่านตัวนี้)
- **`OnModelCreating`** = ตั้งค่าด้วย **Fluent API** (สิ่งที่ attribute ทำไม่ได้):
  - **`HasIndex(...).IsUnique()`** = ★ หัวใจ — สร้าง **unique constraint ที่ระดับฐานข้อมูลจริง**
    - นี่คือ **defense in depth**: แม้โค้ดเช็คซ้ำใน service พลาด (เช่น 2 request พร้อมกัน — race condition) DB จะเป็นด่านสุดท้ายที่ปฏิเสธ
  - **`HasConversion<string>()`** = แปลง enum `Status` เก็บเป็นข้อความ `"Active"`/`"Inactive"` แทนเลข `0/1` → เปิดดูใน DB อ่านรู้เรื่อง + เพิ่มค่า enum ใหม่ไม่ทำให้ค่าเดิมเพี้ยน
  - `HasMaxLength` = จำกัดความยาวคอลัมน์ (`varchar(20)` แทน `text` ยาวไม่จำกัด)

---

### 2.7 `Migrations/InitialCreate.cs` — สร้างตารางจากโค้ด

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(name: "Students", columns: table => new {
        Id = table.Column<int>(...).Annotation("Npgsql:ValueGenerationStrategy", IdentityByDefaultColumn),
        StudentNo = table.Column<string>(maxLength: 20, nullable: false),
        ...
    }, constraints: table => table.PrimaryKey("PK_Students", x => x.Id));

    migrationBuilder.CreateIndex("IX_Students_StudentNo", "Students", "StudentNo", unique: true);
}
```

**อธิบาย:**
- **Migration = Code First** — เราออกแบบ model ในโค้ด แล้วให้ EF gen SQL สร้าง schema ให้ (ไม่ต้องเขียน SQL `CREATE TABLE` มือ)
- `Up()` = ตอน apply (สร้างตาราง+index unique) / `Down()` = ตอน rollback (drop ตาราง)
- `IdentityByDefaultColumn` = คอลัมน์ Id เป็น auto-increment (PostgreSQL `GENERATED BY DEFAULT AS IDENTITY`)
- คำสั่งที่ใช้: `dotnet ef migrations add InitialCreate` แล้ว `dotnet ef database update`

---

### 2.8 Config & Infra

**`appsettings.json`** — connection string:
```json
"DefaultConnection": "Host=localhost;Port=5433;Database=studentdb;Username=postgres;Password=postgres"
```
- Port **5433** (host) → map ไป 5432 (ใน container) ดู docker-compose
- `appsettings.Development.json` = override เฉพาะตอน dev (config ซ้อนกัน ตัวเจาะจง env ชนะ)

**`docker-compose.yml`** — ยก PostgreSQL 17 ขึ้นมา:
- `ports: "5433:5432"` — host 5433 → container 5432 (ตั้ง 5433 กันชนกับ Postgres ที่อาจมีอยู่แล้วบนเครื่อง)
- `volumes: pgdata` — **ข้อมูลไม่หายแม้ลบ container** (persistent storage)
- `healthcheck` (`pg_isready`) — เช็คว่า DB พร้อมรับ connection จริงก่อน
- ส่วน `api:` ยัง comment ไว้ (ตอนเรียน/dev รันจาก Visual Studio สะดวกกว่า)

**`Dockerfile`** — **Multi-stage build**:
- **Stage 1 (build)** ใช้ SDK image (ใหญ่ มีเครื่องมือครบ) → restore + publish
- **Stage 2 (runtime)** ใช้ aspnet image (เล็กกว่า ไม่มี SDK) → copy เฉพาะไฟล์ที่ publish แล้วมารัน
- **ทำไม multi-stage?** → image สุดท้าย **เล็กและปลอดภัยกว่า** (ไม่มี source code / SDK ติดไปด้วย)
- copy `.csproj` ก่อนแล้ว `restore` ก่อน copy โค้ดที่เหลือ → ใช้ **Docker layer caching** (ถ้า dependency ไม่เปลี่ยน ไม่ต้อง restore ใหม่ = build เร็วขึ้น)

**`StudentAPI.csproj`:**
- `net10.0`, `<Nullable>enable</Nullable>` (กัน null bug), `<ImplicitUsings>enable</ImplicitUsings>` (ไม่ต้อง using ซ้ำๆ)
- Packages: `Npgsql.EntityFrameworkCore.PostgreSQL` (provider), `EntityFrameworkCore.Design` (สำหรับ migration), `Swashbuckle` (Swagger)

---

## 3. Data Flow: ตาม 1 request ตั้งแต่ต้นจนจบ

**ตัวอย่าง: `POST /Students` ด้วย body `{ "studentNo": "  6501  ", "studentName": "Ann", "level": "ป.ตรี" }`**

```
1. HTTP POST เข้ามา → Kestrel → Middleware pipeline (HTTPS redirect, Auth)
2. Routing เจอ /Students + POST → เข้า StudentsController.Create
3. [ApiController] deserialize JSON → object Student + เช็ค [Required]
      ผ่าน validation (ทุก field มีค่า) → เข้า method
4. Controller เรียก _service.CreateAsync(student)   ← DI ฉีด StudentService มาแล้ว
5. Service:
      - Trim: "  6501  " → "6501"
      - AnyAsync เช็คซ้ำ → EF แปลงเป็น SELECT EXISTS → PostgreSQL ตอบ false
      - Students.Add(student) + SaveChangesAsync → EF ยิง INSERT → DB gen Id = 1
      - return Success(student)  (Status=Success, Value=student ที่มี Id แล้ว)
6. Controller: result.Status != Duplicate → return CreatedAtAction(...)
7. ออกไปเป็น: HTTP 201 Created
      Header: Location: /Students/1
      Body: { "id":1, "studentNo":"6501", "studentName":"Ann", "level":"ป.ตรี", "status":"Active" }
```

**ถ้า StudentNo ซ้ำ:** ขั้น 5 `exists == true` → คืน `Duplicate` → Controller คืน `400` + ProblemDetails
**ถ้า StudentNo ว่าง:** ขั้น 3 `[ApiController]` เด้ง `400` อัตโนมัติ ไม่เข้า service เลย

---

## 4. แนวคิด/เทคนิคสำคัญที่ต้องอธิบายให้ได้

| แนวคิด | อยู่ที่ไหน | อธิบายสั้นๆ |
|--------|-----------|-------------|
| **Dependency Injection** | `Program.cs`, constructor ทุกคลาส | ลงทะเบียน service ไว้ container ฉีดให้ ไม่ new เอง |
| **Layered Architecture** | Controller/Service/Data | แยกหน้าที่ แต่ละชั้นทำงานเดียว |
| **Result Pattern** | `StudentServiceResult` | คืนผลแบบมี status แทน throw exception |
| **Repository/Unit of Work** | `AppDbContext`, `DbSet` | EF ให้มาในตัว |
| **Code First + Migration** | `Migrations/` | ออกแบบใน model แล้ว gen schema |
| **async/await** | ทุก method DB | ไม่บล็อก thread ระหว่างรอ I/O → รองรับ concurrent สูง |
| **Deferred Execution** | `GetAllAsync` | ต่อ `.Where()` ได้จนกว่าจะ `ToListAsync` |
| **AsNoTracking** | `GetAllAsync` | query อ่านอย่างเดียว เร็วขึ้น |
| **Defense in depth (unique)** | เช็คใน service + unique index ใน DB | 2 ชั้นกันข้อมูลซ้ำ |
| **DTO/Entity, Validation** | `Student.cs` | Data Annotations + `[ApiController]` เช็คให้ |
| **RESTful design** | Controller | status code, verb, CreatedAtAction ถูกหลัก |

**async/await — ทำไมสำคัญ?**
ทุก method ที่แตะ DB เป็น `async` และ `await` งาน I/O (`ToListAsync`, `SaveChangesAsync`, `FindAsync`)
→ ระหว่างรอ DB ตอบ **thread ถูกปล่อยไปรับ request อื่น** ไม่ยืนรอเปล่าๆ
→ server รองรับผู้ใช้พร้อมกันได้เยอะขึ้นด้วย thread เท่าเดิม (scalability)

---

## 5. คำถามสัมภาษณ์ที่น่าจะโดน + คำตอบ

**Q: เล่าสถาปัตยกรรมโปรเจกต์นี้หน่อย**
> เป็น Web API แบ่ง 3 ชั้น: Controller รับ HTTP อย่างเดียว, Service เก็บ business logic ทั้งหมด, DbContext คุยกับ DB ผ่าน EF Core. Controller พึ่ง interface `IStudentService` ผ่าน DI ทำให้เทสง่ายและสลับ implementation ได้

**Q: `[ApiController]` ทำอะไร**
> เปิด auto model validation (ModelState ไม่ผ่านคืน 400 เอง), auto bind จาก body, และบังคับ attribute routing ทำให้ controller โค้ดสั้นลง

**Q: ทำไม Service คืน `StudentServiceResult` แทนคืน `Student` ตรงๆ หรือ throw**
> เพราะต้องแยกกรณี "ไม่พบ" กับ "ซ้ำ" ให้ Controller แปลเป็น 404/400 ได้ถูก โดยที่ Service ไม่ต้องรู้จัก HTTP เลย และไม่ใช้ exception กับ flow ปกติของธุรกิจ (เปลือง + ช้า)

**Q: กันข้อมูลซ้ำยังไง**
> 2 ชั้น: (1) Service เช็คด้วย `AnyAsync` ก่อน insert เพื่อคืน error message ที่เป็นมิตร (2) `HasIndex().IsUnique()` สร้าง unique constraint ที่ DB เป็นด่านสุดท้ายกัน race condition — ถ้ามีแค่ชั้นแรก 2 request พร้อมกันอาจ insert ซ้ำได้

**Q: `AsNoTracking()` คืออะไร ใช้ตอนไหน**
> ปิด change tracking ของ EF สำหรับ query ที่แค่อ่านไม่แก้ → เร็วขึ้น กิน memory น้อยลง ใช้ใน GetAll/list ทั้งหลาย

**Q: `ILike` กับ `Like` ต่างกันยังไง**
> `ILIKE` เป็นของ PostgreSQL ค้นแบบ case-insensitive ส่วน `LIKE` สนตัวพิมพ์เล็กใหญ่ ผมใช้ `EF.Functions.ILike` ให้ค้นชื่อไม่ต้องแคร์ตัวพิมพ์

**Q: async/await ช่วยอะไร**
> ระหว่างรอ I/O (DB) thread ถูกปล่อยไปทำงานอื่น ไม่บล็อก → server รับ request พร้อมกันได้มากขึ้นด้วยทรัพยากรเท่าเดิม

**Q: Migration คืออะไร รันยังไง**
> เครื่องมือ EF ที่ gen schema DB จาก model (code first) `dotnet ef migrations add <ชื่อ>` แล้ว `dotnet ef database update` มี Up (apply) / Down (rollback)

**Q: Scoped vs Singleton vs Transient ต่างกันไง**
> Scoped = 1 instance ต่อ request (DbContext ต้องเป็นแบบนี้ เพราะไม่ thread-safe และควรมี unit of work ต่อ request), Singleton = 1 ตัวตลอด app, Transient = สร้างใหม่ทุกครั้งที่ขอ

**Q: PUT กับ PATCH ต่างกันไง โปรเจกต์นี้ใช้อะไร**
> ใช้ PUT = แทนที่ทั้ง resource (ส่งทุก field มา) ส่วน PATCH = แก้บางส่วน โปรเจกต์นี้ Update รับ object เต็มแล้ว set ทุก field จึงเป็น PUT

**Q: ทำไม `student.Id = 0` ตอน Create**
> กันคน client แอบส่ง Id มาเอง บังคับให้ DB generate ให้ (identity column)

---

## 6. จุดที่ปรับปรุงได้

ถ้ากรรมการถาม "จะทำให้โปรดักชันพร้อมขึ้นยังไง" ตอบได้ว่า:

1. **แยก DTO ออกจาก Entity** — ตอนนี้ใช้ `Student` ทั้งรับ input และ entity เดียวกัน ควรมี `CreateStudentDto`/`StudentResponseDto` เพื่อ (ก) ซ่อน field ที่ไม่อยากให้ client เห็น/ส่ง (ข) ให้ input/output วิวัฒน์แยกจาก schema DB
2. **Pagination** — `GetAll` คืนทั้งหมด ถ้าข้อมูลล้านแถวจะพัง ควรมี `page`/`pageSize` + `Skip/Take`
3. **จัดการ DB unique violation** — ถ้าชน unique index ที่ DB (race) ตอนนี้จะเด้ง exception 500 ควร catch `DbUpdateException` แล้วคืน 409 Conflict ให้สวย
4. **Global exception handling** — เพิ่ม middleware/`IExceptionHandler` แปลง exception เป็น ProblemDetails ทั้งระบบ
5. **Logging** — ใส่ `ILogger` log operation สำคัญ
6. **Authentication/Authorization** — ตอนนี้ `UseAuthorization` มีแต่ยังไม่มี auth จริง ควรเพิ่ม JWT
7. **Unit / Integration tests** — interface `IStudentService` mock ได้อยู่แล้ว ควรเขียนเทสจริง
8. **`409 Conflict` แทน `400`** สำหรับ duplicate — ตามความหมาย HTTP จริงๆ duplicate คือ conflict มากกว่า bad request

---

## สรุป 30 วินาที (พูดตอนเปิดสัมภาษณ์)

> "โปรเจกต์นี้เป็น REST API จัดการข้อมูลนักศึกษา เขียนด้วย ASP.NET Core .NET 10 + EF Core ต่อ PostgreSQL รันด้วย Docker แบ่งเป็น 3 ชั้นชัดเจน — Controller รับ HTTP, Service เก็บ business logic แล้วคืนผลแบบ Result Pattern, DbContext จัดการ data ผ่าน EF. จุดเด่นคือกันข้อมูล StudentNo ซ้ำ 2 ชั้น (โค้ด + unique index ที่ DB), รองรับค้นหาแบบ case-insensitive, ทุก DB call เป็น async, และใช้ DI + interface ทำให้เทสง่าย"
