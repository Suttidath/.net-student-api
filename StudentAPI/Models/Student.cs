using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StudentAPI.Models
{
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
}
