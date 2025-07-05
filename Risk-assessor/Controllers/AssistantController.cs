using Microsoft.AspNetCore.Mvc;
using Risk_assessor.Services;

namespace Risk_assessor.Controllers

{
    [ApiController]
    [Route("api/[controller]")]
    public class AssistantController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        private readonly OpenAIAssistantService _openAIAssistantService;

        public AssistantController(OpenAIAssistantService openAIAssistantService)
        {
            _openAIAssistantService = openAIAssistantService;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskAssistant([FromBody] string question)
        {
            var response = await _openAIAssistantService.GetAssistantResponseAsync(question);
            return Ok(response);
        }
    }
}
