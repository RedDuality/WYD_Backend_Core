using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Core.Services.Util;

public static class ExceptionService
{
    public static IActionResult GetErrorResult(Exception e)
    {
        return e switch
        {
            UnauthorizedAccessException unauthorizedEx => new UnauthorizedObjectResult(
                unauthorizedEx.Message
            ),
            SecurityTokenValidationException securityTokenValidationException =>
                new UnauthorizedObjectResult(securityTokenValidationException.Message),
            InvalidOperationException invalidOperationException => new BadRequestObjectResult(
                invalidOperationException.Message
            ),

            ArgumentNullException argumentNullException => new BadRequestObjectResult(
                "Expected a value but none was given"
            ),
            KeyNotFoundException keyNotFoundEx => new NotFoundObjectResult(
                keyNotFoundEx.Message + " with given Id not found"
            ),

            FormatException formatEx => new BadRequestObjectResult("Id Format wrong"),
            OverflowException overflowEx => new BadRequestObjectResult("Id Format wrong"),
            ArgumentException argumentException => new BadRequestObjectResult(
                "Input error" + argumentException.Message
            ),
            JsonException jsonException => new BadRequestObjectResult(
                "Body malformed: " + jsonException.Message
            ),
            ThreadInterruptedException => new StatusCodeResult(StatusCodes.Status504GatewayTimeout),
            _ => new StatusCodeResult(StatusCodes.Status500InternalServerError),
        };
    }
}