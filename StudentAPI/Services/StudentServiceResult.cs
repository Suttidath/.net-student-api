namespace StudentAPI.Services
{
    // ผลลัพธ์แบบง่ายจาก Service เพื่อบอก Controller ว่าเกิดอะไรขึ้น
    // (สำเร็จ / ไม่พบ / ข้อมูลซ้ำ) โดยไม่ต้องผูกกับ HTTP ในชั้น Service
    public enum StudentResultStatus
    {
        Success,
        NotFound,
        Duplicate
    }

    public class StudentServiceResult<T>
    {
        public StudentResultStatus Status { get; init; }
        public T? Value { get; init; }
        public string? Error { get; init; }

        public bool IsSuccess => Status == StudentResultStatus.Success;

        public static StudentServiceResult<T> Success(T value) =>
            new() { Status = StudentResultStatus.Success, Value = value };

        public static StudentServiceResult<T> NotFound(string error) =>
            new() { Status = StudentResultStatus.NotFound, Error = error };

        public static StudentServiceResult<T> Duplicate(string error) =>
            new() { Status = StudentResultStatus.Duplicate, Error = error };
    }
}
