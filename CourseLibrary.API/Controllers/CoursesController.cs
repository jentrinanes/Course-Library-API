using AutoMapper;
using CourseLibrary.API.Entities;
using CourseLibrary.API.Models;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CourseLibrary.API.Controllers
{
    [ApiController]
    [Route("api/authors/{authorId}/courses")]    
    public class CoursesController : ControllerBase
    {
        private readonly ICourseLibraryRepository _repo;
        private readonly IMapper _mapper;

        public CoursesController(ICourseLibraryRepository repo, IMapper mapper)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// Get all courses by author Id
        /// </summary>
        /// <param name="authorId">The author Id</param>
        /// <returns></returns>
        [HttpGet(Name = "GetCoursesForAuthor")]
        public async Task<IActionResult> GetCoursesForAuthor(Guid authorId)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var courses = await _repo.GetCoursesAsync(authorId);
            return Ok(_mapper.Map<IEnumerable<CourseDto>>(courses));
        }

        /// <summary>
        /// Get a course by author Id
        /// </summary>
        /// <param name="authorId">The author Id</param>
        /// <param name="courseId">The course Id</param>
        /// <returns></returns>
        [HttpGet("{courseId}", Name = "GetCourseForAuthor")]        
        public async Task<IActionResult> GetCourseForAuthor(Guid authorId, Guid courseId)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = await _repo.GetCourseAsync(authorId, courseId);

            if (course == null)
                return NotFound();

            return Ok(_mapper.Map<CourseDto>(course));
        }

        /// <summary>
        /// Create a course for an author
        /// </summary>
        /// <param name="authorId">The author Id</param>
        /// <param name="courseForCreationDto">The payload for creating a course</param>
        /// <returns></returns>
        [HttpPost(Name = "CreateCourseForAuthor")]
        public async Task<IActionResult> CreateCourseForAuthor(Guid authorId, CourseForCreationDto courseForCreationDto)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = _mapper.Map<Course>(courseForCreationDto);
            
            _repo.AddCourse(authorId, course);
            await _repo.SaveChangesAsync();

            var result = _mapper.Map<CourseDto>(course);
            return CreatedAtRoute("GetCourseForAuthor", new { authorId, courseId = result.Id }, result);
        }

        /// <summary>
        /// Update a course for an author
        /// </summary>
        /// <param name="authorId">The author Id</param>
        /// <param name="courseId">The course Id</param>
        /// <param name="courseForUpdateDto">The payload for updating a course</param>
        /// <returns></returns>
        [HttpPut("{courseId}")]
        public async Task<IActionResult> UpdateCourseForAuthor(Guid authorId, Guid courseId, CourseForUpdateDto courseForUpdateDto)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = await _repo.GetCourseAsync(authorId, courseId);

            if (course == null)
            {
                var courseToAdd = _mapper.Map<Course>(courseForUpdateDto);
                courseToAdd.Id = courseId;

                _repo.AddCourse(authorId, courseToAdd);
                await _repo.SaveChangesAsync();

                var result = _mapper.Map<CourseDto>(courseToAdd);
                return CreatedAtRoute("GetCourseForAuthor", new { authorId, courseId = result.Id }, result);
            }               

            _mapper.Map(courseForUpdateDto, course);
            _repo.UpdateCourse(course);
            await _repo.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Partially update a course for an author
        /// </summary>
        /// <param name="authorId">The author Id</param>
        /// <param name="courseId">The course Id</param>
        /// <param name="patchDocument">The payload for partially updating a course</param>
        /// <returns></returns>
        [HttpPatch("{courseId}")]
        public async Task<IActionResult> PartiallyUpdateCourseForAuthor(Guid authorId, Guid courseId, JsonPatchDocument<CourseForUpdateDto> patchDocument)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = await _repo.GetCourseAsync(authorId, courseId);

            if (course == null)
            {
                var courseDto = new CourseForUpdateDto();
                patchDocument.ApplyTo(courseDto, ModelState);

                if (!TryValidateModel(courseDto))
                    return ValidationProblem(ModelState);

                var courseToAdd = _mapper.Map<Course>(courseDto);
                courseToAdd.Id = courseId;

                _repo.AddCourse(authorId, courseToAdd);
                await _repo.SaveChangesAsync();

                var result = _mapper.Map<CourseDto>(courseToAdd);
                return CreatedAtRoute("GetCourseForAuthor", new { authorId, courseId = result.Id }, result);
            }                

            var courseToPatch = _mapper.Map<CourseForUpdateDto>(course);
            patchDocument.ApplyTo(courseToPatch, ModelState);

            if (!TryValidateModel(courseToPatch))            
                return ValidationProblem(ModelState);
            
            _mapper.Map(courseToPatch, course);
            _repo.UpdateCourse(course);
            await _repo.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Delete a course for an author
        /// </summary>
        /// <param name="authorId">The author Id</param>
        /// <param name="courseId">The course Id</param>
        /// <returns></returns>
        [HttpDelete("{courseId}")]
        public async Task<IActionResult> DeleteCourseForAuthor(Guid authorId, Guid courseId)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = await _repo.GetCourseAsync(authorId, courseId);

            if (course == null)
                return NotFound();

            _repo.DeleteCourse(course);
            await _repo.SaveChangesAsync();

            return NoContent();
        }

        public override ActionResult ValidationProblem([ActionResultObjectValue] ModelStateDictionary modelStateDictionary)
        {
            var options = HttpContext.RequestServices.GetRequiredService<IOptions<ApiBehaviorOptions>>();
            return (ActionResult)options.Value.InvalidModelStateResponseFactory(ControllerContext);
        }
    }
}
