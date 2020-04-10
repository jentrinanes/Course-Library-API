﻿using AutoMapper;
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

namespace CourseLibrary.API.Controllers
{
    [ApiController]
    [Route("api/authors/{authorId}/courses")]
    [ResponseCache(CacheProfileName = "240SecondsCacheProfile")]
    public class CoursesController : ControllerBase
    {
        private readonly ICourseLibraryRepository _repo;
        private readonly IMapper _mapper;

        public CoursesController(ICourseLibraryRepository repo, IMapper mapper)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        [HttpGet(Name = "GetCoursesForAuthor")]
        public IActionResult GetCoursesForAuthor(Guid authorId)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var courses = _repo.GetCourses(authorId);
            return Ok(_mapper.Map<IEnumerable<CourseDto>>(courses));
        }

        [HttpGet("{courseId}", Name = "GetCourseForAuthor")]
        [ResponseCache(Duration = 120)]
        public IActionResult GetCourseForAuthor(Guid authorId, Guid courseId)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = _repo.GetCourse(authorId, courseId);

            if (course == null)
                return NotFound();

            return Ok(_mapper.Map<CourseDto>(course));
        }

        [HttpPost(Name = "CreateCourseForAuthor")]
        public IActionResult CreateCourseForAuthor(Guid authorId, CourseForCreationDto courseForCreationDto)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = _mapper.Map<Course>(courseForCreationDto);
            
            _repo.AddCourse(authorId, course);
            _repo.Save();

            var result = _mapper.Map<CourseDto>(course);
            return CreatedAtRoute("GetCourseForAuthor", new { authorId, courseId = result.Id }, result);
        }

        [HttpPut("{courseId}")]
        public IActionResult UpdateCourseForAuthor(Guid authorId, Guid courseId, CourseForUpdateDto courseForUpdateDto)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = _repo.GetCourse(authorId, courseId);

            if (course == null)
            {
                var courseToAdd = _mapper.Map<Course>(courseForUpdateDto);
                courseToAdd.Id = courseId;

                _repo.AddCourse(authorId, courseToAdd);
                _repo.Save();

                var result = _mapper.Map<CourseDto>(courseToAdd);
                return CreatedAtRoute("GetCourseForAuthor", new { authorId, courseId = result.Id }, result);
            }               

            _mapper.Map(courseForUpdateDto, course);
            _repo.UpdateCourse(course);
            _repo.Save();

            return NoContent();
        }

        [HttpPatch("{courseId}")]
        public IActionResult PartiallyUpdateCourseForAuthor(Guid authorId, Guid courseId, JsonPatchDocument<CourseForUpdateDto> patchDocument)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = _repo.GetCourse(authorId, courseId);

            if (course == null)
            {
                var courseDto = new CourseForUpdateDto();
                patchDocument.ApplyTo(courseDto, ModelState);

                if (!TryValidateModel(courseDto))
                    return ValidationProblem(ModelState);

                var courseToAdd = _mapper.Map<Course>(courseDto);
                courseToAdd.Id = courseId;

                _repo.AddCourse(authorId, courseToAdd);
                _repo.Save();

                var result = _mapper.Map<CourseDto>(courseToAdd);
                return CreatedAtRoute("GetCourseForAuthor", new { authorId, courseId = result.Id }, result);
            }                

            var courseToPatch = _mapper.Map<CourseForUpdateDto>(course);
            patchDocument.ApplyTo(courseToPatch, ModelState);

            if (!TryValidateModel(courseToPatch))            
                return ValidationProblem(ModelState);
            
            _mapper.Map(courseToPatch, course);
            _repo.UpdateCourse(course);
            _repo.Save();

            return NoContent();
        }

        [HttpDelete("{courseId}")]
        public IActionResult DeleteCourseForAuthor(Guid authorId, Guid courseId)
        {
            if (!_repo.AuthorExists(authorId))
                return NotFound();

            var course = _repo.GetCourse(authorId, courseId);

            if (course == null)
                return NotFound();

            _repo.DeleteCourse(course);
            _repo.Save();

            return NoContent();
        }

        public override ActionResult ValidationProblem([ActionResultObjectValue] ModelStateDictionary modelStateDictionary)
        {
            var options = HttpContext.RequestServices.GetRequiredService<IOptions<ApiBehaviorOptions>>();
            return (ActionResult)options.Value.InvalidModelStateResponseFactory(ControllerContext);
        }
    }
}
