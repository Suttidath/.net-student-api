using StudentAPI.Models;

namespace StudentAPI.Services
{
    // สัญญา (interface) ของ Student Service — รวม CRUD ทั้งหมดไว้ที่นี่
    public interface IStudentService
    {
        // อ่านทั้งหมด (search = ค้นทั้ง studentNo และ studentName ในช่องเดียว, filter ด้วย status / level)
        Task<IEnumerable<Student>> GetAllAsync(string? search, StudentStatus? status, string? level);

        // อ่านตาม id
        Task<StudentServiceResult<Student>> GetByIdAsync(int id);

        // เพิ่มใหม่
        Task<StudentServiceResult<Student>> CreateAsync(Student student);

        // แก้ไข
        Task<StudentServiceResult<Student>> UpdateAsync(int id, Student input);

        // ลบ
        Task<StudentServiceResult<Student>> DeleteAsync(int id);
    }
}
