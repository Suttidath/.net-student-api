using Microsoft.EntityFrameworkCore;
using StudentAPI.Data;
using StudentAPI.Models;

namespace StudentAPI.Services
{
    // ชั้น Service: รวม logic CRUD + กฎธุรกิจ (เช่น ห้าม StudentNo ซ้ำ)
    // Controller จะเรียกใช้ที่นี่ แทนการยุ่งกับ AppDbContext โดยตรง
    public class StudentService : IStudentService
    {
        private readonly AppDbContext _db;

        public StudentService(AppDbContext db)
        {
            _db = db;
        }

        // GET ทั้งหมด — ค้นแบบบางส่วน + ไม่สนตัวพิมพ์เล็ก/ใหญ่ (ILike ของ PostgreSQL)
        public async Task<IEnumerable<Student>> GetAllAsync(string? studentNo, string? studentName)
        {
            var query = _db.Students.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(studentNo))
                query = query.Where(s => EF.Functions.ILike(s.StudentNo, $"%{studentNo.Trim()}%"));

            if (!string.IsNullOrWhiteSpace(studentName))
                query = query.Where(s => EF.Functions.ILike(s.StudentName, $"%{studentName.Trim()}%"));

            return await query.OrderBy(s => s.Id).ToListAsync();
        }

        // GET ตาม id
        public async Task<StudentServiceResult<Student>> GetByIdAsync(int id)
        {
            var student = await _db.Students.FindAsync(id);
            if (student is null)
                return StudentServiceResult<Student>.NotFound($"ไม่พบนักศึกษา id = {id}");

            return StudentServiceResult<Student>.Success(student);
        }

        // POST — เพิ่มใหม่ (เช็ค StudentNo ห้ามซ้ำ)
        public async Task<StudentServiceResult<Student>> CreateAsync(Student student)
        {
            student.StudentNo = student.StudentNo.Trim();
            student.StudentName = student.StudentName.Trim();
            student.Level = student.Level.Trim();

            var exists = await _db.Students
                .AnyAsync(s => s.StudentNo.ToLower() == student.StudentNo.ToLower());
            if (exists)
                return StudentServiceResult<Student>.Duplicate(
                    $"Student No '{student.StudentNo}' นี้มีอยู่แล้ว (ห้ามซ้ำ)");

            student.Id = 0; // ให้ DB gen id เอง
            _db.Students.Add(student);
            await _db.SaveChangesAsync();

            return StudentServiceResult<Student>.Success(student);
        }

        // PUT — แก้ไข (เช็ค StudentNo ห้ามซ้ำกับคนอื่น)
        public async Task<StudentServiceResult<Student>> UpdateAsync(int id, Student input)
        {
            var student = await _db.Students.FindAsync(id);
            if (student is null)
                return StudentServiceResult<Student>.NotFound($"ไม่พบนักศึกษา id = {id}");

            var newStudentNo = input.StudentNo.Trim();

            var duplicated = await _db.Students
                .AnyAsync(s => s.Id != id && s.StudentNo.ToLower() == newStudentNo.ToLower());
            if (duplicated)
                return StudentServiceResult<Student>.Duplicate(
                    $"Student No '{newStudentNo}' นี้มีอยู่แล้ว (ห้ามซ้ำ)");

            student.StudentNo = newStudentNo;
            student.StudentName = input.StudentName.Trim();
            student.Level = input.Level.Trim();
            student.Status = input.Status;

            await _db.SaveChangesAsync();
            return StudentServiceResult<Student>.Success(student);
        }

        // DELETE — ลบ
        public async Task<StudentServiceResult<Student>> DeleteAsync(int id)
        {
            var student = await _db.Students.FindAsync(id);
            if (student is null)
                return StudentServiceResult<Student>.NotFound($"ไม่พบนักศึกษา id = {id}");

            _db.Students.Remove(student);
            await _db.SaveChangesAsync();

            return StudentServiceResult<Student>.Success(student);
        }
    }
}
