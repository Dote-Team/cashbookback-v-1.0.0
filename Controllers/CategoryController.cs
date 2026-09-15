using AutoMapper;
using cashbook.Dto;
using cashbook.Models;
using cashbook.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using cashbook.Dto.category;
using cashbook.Dto.category;
using cashbook.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using cashbook.Data;
using Swashbuckle.AspNetCore.Annotations;

namespace cashbook.Controllers
{
    [Route("API/[Controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        protected APIResponse _response;
        private readonly IMapper _mapper;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;

        public CategoryController(ICategoryRepository categoryRepository, IMapper mapper , ApplicationDbContext context)
        {
            _categoryRepository = categoryRepository;
            _response = new();
            _mapper = mapper;
            _context = context;
        }

        // GET: API/Category
        [HttpGet]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "all")]

        public async Task<ActionResult<APIResponse>> GetPaginatedCategorys(
      [FromQuery] Guid businessId,
      [FromQuery] int? skip = 1,
      [FromQuery] int? take = 25,
      [FromQuery] string? search = null)
        {
            try
            {
               

                var paginatedCategorys = await _categoryRepository.GetCategoriesAsync(
                    businessId,
                    skip,
                    take,
                    search);

                var paginatedResponse = new PaginatedResponse<CategoryDto>
                {
                    Data = paginatedCategorys.Data,
                    TotalRecords = paginatedCategorys.TotalRecords,
                    Skip = paginatedCategorys.Skip,
                    Take = paginatedCategorys.Take
                };

                _response.Result = paginatedResponse;
                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;

                return Ok(_response);
            }
            catch (UnauthorizedAccessException uaex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.ErrorMessages = new List<string> { uaex.Message };
                return StatusCode(StatusCodes.Status403Forbidden, _response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.ToString() };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }

        // GET: API/Category/{id}
        [HttpGet("{id:Guid}", Name = "GetCategory")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> GetCategory(Guid id)
        {
            try
            {
                if (id == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    return BadRequest(_response);
                }

                var category = await _categoryRepository.GetAsync(u => u.Id == id);
                if (category == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    return NotFound(_response);
                }

                _response.Result = category;
                _response.StatusCode = HttpStatusCode.OK;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        // POST: API/Category
        [HttpPost]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> CreateCategory([FromBody] CreateCategoryDto createCategoryDto)
        {
            try
            {

                if (createCategoryDto == null)
                {
                    return BadRequest(createCategoryDto);
                }
                var category = _mapper.Map<Category>(createCategoryDto);

                await _categoryRepository.CreateAsync(category);
                _response.Result = new { Id = category.Id };
                _response.StatusCode = HttpStatusCode.Created;
                _response.IsSuccess = true;

                return Ok(_response);

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        [HttpDelete("{id:Guid}", Name = "DeleteCategory")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> DeleteCategory(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid category ID.");
                }

                var category = await _categoryRepository.GetAsync(u => u.Id == id);
                if (category == null)
                {
                    return NotFound("Category not found.");
                }


                await _categoryRepository.RemoveAsync(category);

                _response.StatusCode = HttpStatusCode.NoContent;
                _response.IsSuccess = true;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(500, _response);
            }
        }


        [HttpPut("{id:Guid}", Name = "UpdateCategory")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> UpdateCategory([FromRoute] Guid id, [FromBody] UpdateCategoryDto updateCategoryDto)
        {
            try
            {
                var existingCategory = await _categoryRepository.GetAsync(u => u.Id == id);
                _mapper.Map(updateCategoryDto, existingCategory);

                if (updateCategoryDto == null || existingCategory == null)
                {
                    return BadRequest();
                }

                await _categoryRepository.UpdateAsync(existingCategory);
                _response.Result = existingCategory;
                _response.StatusCode = HttpStatusCode.NoContent;
                _response.IsSuccess = true;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }
    }
}
