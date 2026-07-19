using Microsoft.EntityFrameworkCore;
using StudentAPI.Models;

namespace StudentAPI.Data
{
    // AppDbContext = ตัวแทนของฐานข้อมูลในโค้ด
    // แต่ละ DbSet<> = หนึ่งตารางในฐานข้อมูล
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // ตาราง Students
        public DbSet<Student> Students => Set<Student>();

        // ที่สำหรับตั้งกฎเพิ่มเติมให้แต่ละตาราง (ที่ attribute ทำไม่ได้)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Student>(entity =>
            {
                // ★ หัวใจ: StudentNo ห้ามซ้ำ — บังคับที่ระดับฐานข้อมูลจริง
                entity.HasIndex(s => s.StudentNo)
                      .IsUnique();

                // จำกัดความยาวคอลัมน์ (แทน text ยาวไม่จำกัด)
                entity.Property(s => s.StudentNo).HasMaxLength(20);
                entity.Property(s => s.StudentName).HasMaxLength(100);
                entity.Property(s => s.Level).HasMaxLength(50);

                // เก็บ enum Status เป็นข้อความ "Active"/"Inactive" ใน DB (อ่านง่ายกว่าเลข 0/1)
                entity.Property(s => s.Status)
                      .HasConversion<string>()
                      .HasMaxLength(20);
            });
        }
    }
}
