using AutoMapper;
using CourseLibrary.API.Helpers;
using CourseLibrary.API.Models;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CourseLibrary.API.Controllers
{
    [ApiController]
    [Route("api/authorcollections")]
    public class AuthorCollectionsController : ControllerBase
    {
        private readonly ICourseLibraryRepository _repo;
        private readonly IMapper _mapper;

        public AuthorCollectionsController(ICourseLibraryRepository repo, IMapper mapper)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// Gets the authors by a collection of author Ids
        /// </summary>
        /// <param name="ids">A collection of author Ids</param>
        /// <returns></returns>
        [HttpGet("({ids})", Name = "GetAuthorCollection")]
        public IActionResult GetAuthorCollection([FromRoute] [ModelBinder(BinderType = typeof(ArrayModelBinder))] IEnumerable<Guid> ids)
        {
            if (ids == null)
                return BadRequest();

            var authors = _repo.GetAuthors(ids);

            if (ids.Count() != authors.Count())
                return NotFound();

            var result = _mapper.Map<IEnumerable<AuthorDto>>(authors);
            return Ok(result);
        }

        /// <summary>
        /// Create a collection of authors
        /// </summary>
        /// <param name="authorForCreationDtos">A collection of authors</param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult CreateAuthorCollection(IEnumerable<AuthorForCreationDto> authorForCreationDtos)
        {
            var authors = _mapper.Map<IEnumerable<Entities.Author>>(authorForCreationDtos);
            foreach(var author in authors) {
                _repo.AddAuthor(author);
            }

            _repo.Save();
            
            var result = _mapper.Map<IEnumerable<AuthorDto>>(authors);
            var idsAsString = string.Join(",", result.Select(a => a.Id));

            return CreatedAtRoute("GetAuthorCollection", new { ids = idsAsString }, result);
;        }
    }
}
