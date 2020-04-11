using AutoMapper;
using CourseLibrary.API.ActionConstraints;
using CourseLibrary.API.Entities;
using CourseLibrary.API.Helpers;
using CourseLibrary.API.Models;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CourseLibrary.API.Controllers
{
    [ApiController]
    [Route("api/authors")]
    public class AuthorsController : ControllerBase
    {
        private readonly ICourseLibraryRepository _repo;
        private readonly IMapper _mapper;
        private readonly IPropertyMappingService _propertyMappingService;
        private readonly IPropertyCheckerService _propertyCheckerService;

        public AuthorsController(ICourseLibraryRepository repo, IMapper mapper, IPropertyMappingService propertyMappingService, IPropertyCheckerService propertyCheckerService)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _propertyMappingService = propertyMappingService ?? throw new ArgumentNullException(nameof(propertyMappingService));
            _propertyCheckerService = propertyCheckerService ?? throw new ArgumentNullException(nameof(propertyCheckerService));
        }

        /// <summary>
        /// Get authors
        /// </summary>
        /// <param name="mainCategory">Filter by main category</param>
        /// <param name="searchQuery">Filter by search query</param>
        /// <param name="fields">Filter columns</param>
        /// <param name="pageNumber">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="orderBy">Order by any field</param>
        /// <returns></returns>
        [HttpGet(Name = "GetAuthors")]
        [HttpHead]
        public IActionResult GetAuthors(string mainCategory, string searchQuery, string fields, int pageNumber = 1, int pageSize = 10, string orderBy = "Name")
        {
            if (!_propertyMappingService.ValidMappingExistsFor<AuthorDto, Author>(orderBy))
                return BadRequest();

            if (!_propertyCheckerService.TypeHasProperties<AuthorDto>(fields))
                return BadRequest();

            var authors = _repo.GetAuthors(mainCategory, searchQuery, pageNumber, pageSize, orderBy);

            var paginationMetadata = new
            {
                totalCount = authors.TotalCount,
                pageSize = authors.PageSize,
                currentPage = authors.CurrentPage,
                totalPages = authors.TotalPages
            };

            Response.Headers.Add("X-Pagination", System.Text.Json.JsonSerializer.Serialize(paginationMetadata));

            var links = CreateLinksForAuthors(mainCategory, searchQuery, fields, pageNumber, pageSize, orderBy,
                authors.HasNext, authors.HasPrevious);

            var shapedAuthors = _mapper.Map<IEnumerable<AuthorDto>>(authors).ShapeData(fields);

            var shapedAuthorsWithLinks = shapedAuthors.Select(author =>
            {
                var authorAsDictionary = author as IDictionary<string, object>;
                var authorLinks = CreateLinksForAuthor((Guid)authorAsDictionary["Id"], null);
                authorAsDictionary.Add("links", authorLinks);
                return authorAsDictionary;
            });

            var linkedCollectionResource = new
            {
                value = shapedAuthorsWithLinks,
                links
            };

            return Ok(linkedCollectionResource);
        }

        /// <summary>
        /// Get an author by authorId
        /// </summary>
        /// <param name="authorId">The ID of the author</param>
        /// <param name="fields">The columns that can be returned</param>
        /// <param name="mediaType">The media type</param>
        /// <returns></returns>
        [Produces("application/json",
            "application/vnd.marvin.hateoas+json",
            "application/vnd.marvin.author.full+json",
            "application/vnd.marvin.author.full.hateoas+json",
            "application/vnd.marvin.author.friendly+json",
            "application/vnd.marvin.author.friendly.hateoas+json")]
        [HttpGet("{authorId}", Name = "GetAuthor")]       
        public async Task<IActionResult> GetAuthor(Guid authorId, string fields, [FromHeader(Name = "Accept")] string mediaType)
        {
            if (!MediaTypeHeaderValue.TryParse(mediaType, out MediaTypeHeaderValue parsedMediaType))
                return BadRequest();
            
            if (!_propertyCheckerService.TypeHasProperties<AuthorDto>(fields))
                return BadRequest();

            var author = await _repo.GetAuthorAsync(authorId);
            if (author == null)
                return NotFound();

            var includeLinks = parsedMediaType.SubTypeWithoutSuffix.EndsWith("hateoas", StringComparison.InvariantCultureIgnoreCase);

            IEnumerable<LinkDto> links = new List<LinkDto>();
            
            if (includeLinks)
                links = CreateLinksForAuthor(authorId, fields);

            var primaryMediaType = includeLinks 
                ? parsedMediaType.SubTypeWithoutSuffix.Substring(0, parsedMediaType.SubTypeWithoutSuffix.Length - 8)
                : parsedMediaType.SubTypeWithoutSuffix;

            if (primaryMediaType == "vnd.marvin.author.full") {
                var fullResourceToReturn = _mapper.Map<AuthorFullDto>(author)
                    .ShapeData(fields) as IDictionary<string, object>;

                if (includeLinks) {
                    fullResourceToReturn.Add("links", links);
                }

                return Ok(fullResourceToReturn);
            }
            
            var friendlyResourceToReturn = _mapper.Map<AuthorDto>(author).ShapeData(fields) as IDictionary<string, object>;

            if (includeLinks)            
                friendlyResourceToReturn.Add("links", links);
            
            return Ok(friendlyResourceToReturn);
        }
        
        /// <summary>
        /// Create an author
        /// </summary>
        /// <param name="authorForCreationDto">The payload for creating an author</param>
        /// <returns></returns>
        [HttpPost(Name = "CreateAuthor")]
        [RequestHeaderMatchesMediaType("Content-Type",
            "application/json",
            "application/vnd.marvin.authorforcreation+json")]
        [Consumes("application/json",
            "application/vnd.marvin.authorforcreation+json")]
        public async Task<IActionResult> CreateAuthor(AuthorForCreationDto authorForCreationDto)
        {            
            var author = _mapper.Map<Entities.Author>(authorForCreationDto);
            _repo.AddAuthor(author);
            await _repo.SaveChangesAsync();

            var response = _mapper.Map<AuthorDto>(author);

            var links = CreateLinksForAuthor(response.Id, null);
            var linkedResourceToReturn = response.ShapeData(null) as IDictionary<string, object>;
            linkedResourceToReturn.Add("links", links);

            return CreatedAtRoute("GetAuthor", new { authorId = linkedResourceToReturn["Id"] }, linkedResourceToReturn);
        }
        
        [HttpOptions]
        public IActionResult GetAuthorsOptions()
        {
            Response.Headers.Add("Allow", "GET,OPTIONS,POST");
            return Ok();
        }

        /// <summary>
        /// Delete an author
        /// </summary>
        /// <param name="authorId">The author Id</param>
        /// <returns></returns>
        [HttpDelete("{authorId}", Name = "DeleteAuthor")]
        public async Task<IActionResult> DeleteAuthor(Guid authorId)
        {
            var author = await _repo.GetAuthorAsync(authorId);

            if (author == null)
                return NotFound();

            _repo.DeleteAuthor(author);
            await _repo.SaveChangesAsync();

            return NoContent();
;        }

        private string CreateAuthorsResourceUri(string mainCategory, string searchQuery, int pageNumber, int pageSize, string orderBy, string fields, ResourceUriType type)
        {
            switch (type)
            {
                case ResourceUriType.PreviousPage:
                    return Url.Link("GetAuthors",
                      new
                      {
                          fields,
                          orderBy,
                          pageNumber = pageNumber - 1,
                          pageSize,
                          mainCategory,
                          searchQuery
                      });
                case ResourceUriType.NextPage:
                    return Url.Link("GetAuthors",
                      new
                      {
                          fields,
                          orderBy,
                          pageNumber = pageNumber + 1,
                          pageSize,
                          mainCategory,
                          searchQuery
                      });
                case ResourceUriType.Current:
                default:
                    return Url.Link("GetAuthors",
                    new
                    {
                        fields,
                        orderBy,
                        pageNumber,
                        pageSize,
                        mainCategory,
                        searchQuery
                    });
            }
        }

        private IEnumerable<LinkDto> CreateLinksForAuthor(Guid authorId, string fields)
        {
            var links = new List<LinkDto>();

            if (string.IsNullOrWhiteSpace(fields)) {
                links.Add(new LinkDto(Url.Link("GetAuthor", new { authorId }), "self", "GET"));
            } else {
                links.Add(new LinkDto(Url.Link("GetAuthor", new { authorId, fields }), "self", "GET"));
            }

            links.Add(new LinkDto(Url.Link("DeleteAuthor", new { authorId }), "delete_author", "DELETE"));

            links.Add(new LinkDto(Url.Link("CreateCourseForAuthor", new { authorId }), "create_course_for_author", "POST"));

            links.Add(new LinkDto(Url.Link("GetCoursesForAuthor", new { authorId }), "courses", "GET"));

            return links;
        }

        private IEnumerable<LinkDto> CreateLinksForAuthors(string mainCategory, string searchQuery, string fields, int pageNumber, 
            int pageSize, string orderBy, bool hasNext, bool hasPrevious)
        {
            var links = new List<LinkDto>();            

            links.Add(new LinkDto(CreateAuthorsResourceUri(mainCategory, searchQuery, pageNumber, pageSize, orderBy, fields, 
                ResourceUriType.Current), "self", "GET"));

            if (hasNext)
            {
                links.Add(new LinkDto(CreateAuthorsResourceUri(mainCategory, searchQuery, pageNumber, pageSize, orderBy, fields,
                    ResourceUriType.NextPage), "nextPage", "GET"));
            }

            if (hasPrevious)
            {
                links.Add(new LinkDto(CreateAuthorsResourceUri(mainCategory, searchQuery, pageNumber, pageSize, orderBy, fields,
                    ResourceUriType.PreviousPage), "previousPage", "GET"));
            }

            return links;
        }
    }
}
