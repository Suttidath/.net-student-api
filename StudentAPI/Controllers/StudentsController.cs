using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;
using StudentAPI.Services;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly IStudentService _service;

        public StudentsController(IStudentService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Student>>> GetAll(
            [FromQuery] string? search,
            [FromQuery] StudentStatus? status,
            [FromQuery] string? level)
        {
            var students = await _service.GetAllAsync(search, status, level);
            return Ok(students);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Student>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result.Status == StudentResultStatus.NotFound)
                return NotFound(new { message = result.Error });

            return Ok(result.Value);
        }

        [HttpPost]
        public async Task<ActionResult<Student>> Create(Student student)
        {
            var result = await _service.CreateAsync(student);
            if (result.Status == StudentResultStatus.Duplicate)
            {
                ModelState.AddModelError(nameof(Student.StudentNo), result.Error!);
                return ValidationProblem(ModelState);
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Student>> Update(int id, Student input)
        {
            var result = await _service.UpdateAsync(id, input);
            if (result.Status == StudentResultStatus.NotFound)
                return NotFound(new { message = result.Error });

            if (result.Status == StudentResultStatus.Duplicate)
            {
                ModelState.AddModelError(nameof(Student.StudentNo), result.Error!);
                return ValidationProblem(ModelState);
            }

            return Ok(result.Value);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            if (result.Status == StudentResultStatus.NotFound)
                return NotFound(new { message = result.Error });

            return NoContent();
        }
    }
}
