using Enter_Extractor_Api.Models;
using Enter_Extractor_Api.Models.Template;
using Enter_Extractor_Api.Services.Template;
using Microsoft.AspNetCore.Mvc;

namespace Enter_Extractor_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(ITemplateService templateService, ILogger<TemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Criar novo template
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ServiceResponse<Models.Template.Template>>> Create([FromBody] CreateTemplateRequest request)
    {
        try
        {
            var userId = "user_default"; 

            var template = await _templateService.CreateAsync(userId, request);

            return Ok(new ServiceResponse<Models.Template.Template>
            {
                Data = template,
                Success = true,
                Message = "Template criado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar template");
            return StatusCode(500, new ServiceResponse<Models.Template.Template>
            {
                Success = false,
                Message = $"Erro ao criar template: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Listar templates do usuário
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ServiceResponse<object>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null)
    {
        try
        {
            var userId = "user_default";

            var (templates, total) = await _templateService.ListUserTemplatesAsync(userId, page, limit, search);

            return Ok(new ServiceResponse<object>
            {
                Data = new
                {
                    templates,
                    total,
                    page,
                    limit,
                    pages = (int)Math.Ceiling(total / (double)limit)
                },
                Success = true,
                Message = $"{templates.Count} templates encontrados"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar templates");
            return StatusCode(500, new ServiceResponse<object>
            {
                Success = false,
                Message = $"Erro ao listar templates: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Obter template específico
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ServiceResponse<Models.Template.Template>>> GetById(string id)
    {
        try
        {
            var userId = "user_default";
            var template = await _templateService.GetByIdAsync(userId, id);

            if (template == null)
            {
                return NotFound(new ServiceResponse<Models.Template.Template>
                {
                    Success = false,
                    Message = "Template não encontrado"
                });
            }

            return Ok(new ServiceResponse<Models.Template.Template>
            {
                Data = template,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter template");
            return StatusCode(500, new ServiceResponse<Models.Template.Template>
            {
                Success = false,
                Message = $"Erro ao obter template: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Atualizar template
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ServiceResponse<Models.Template.Template>>> Update(
        string id,
        [FromBody] UpdateTemplateRequest request)
    {
        try
        {
            var userId = "user_default";
            var template = await _templateService.UpdateAsync(userId, id, request);

            return Ok(new ServiceResponse<Models.Template.Template>
            {
                Data = template,
                Success = true,
                Message = "Template atualizado com sucesso"
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ServiceResponse<Models.Template.Template>
            {
                Success = false,
                Message = "Template não encontrado"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar template");
            return StatusCode(500, new ServiceResponse<Models.Template.Template>
            {
                Success = false,
                Message = $"Erro ao atualizar template: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Deletar template
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ServiceResponse<object>>> Delete(string id)
    {
        try
        {
            var userId = "user_default";
            await _templateService.DeleteAsync(userId, id);

            return Ok(new ServiceResponse<object>
            {
                Success = true,
                Message = "Template deletado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar template");
            return StatusCode(500, new ServiceResponse<object>
            {
                Success = false,
                Message = $"Erro ao deletar template: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Listar templates públicos
    /// </summary>
    [HttpGet("public")]
    public async Task<ActionResult<ServiceResponse<List<Models.Template.Template>>>> ListPublic([FromQuery] int limit = 10)
    {
        try
        {
            var templates = await _templateService.ListPublicTemplatesAsync(limit);

            return Ok(new ServiceResponse<List<Models.Template.Template>>
            {
                Data = templates,
                Success = true,
                Message = $"{templates.Count} templates públicos encontrados"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar templates públicos");
            return StatusCode(500, new ServiceResponse<List<Models.Template.Template>>
            {
                Success = false,
                Message = $"Erro ao listar templates públicos: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Clonar template público
    /// </summary>
    [HttpPost("{id}/clone")]
    public async Task<ActionResult<ServiceResponse<Models.Template.Template>>> Clone(string id)
    {
        try
        {
            var userId = "user_default";
            var template = await _templateService.ClonePublicTemplateAsync(userId, id);

            return Ok(new ServiceResponse<Models.Template.Template>
            {
                Data = template,
                Success = true,
                Message = "Template clonado com sucesso"
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ServiceResponse<Models.Template.Template>
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao clonar template");
            return StatusCode(500, new ServiceResponse<Models.Template.Template>
            {
                Success = false,
                Message = $"Erro ao clonar template: {ex.Message}"
            });
        }
    }
}
