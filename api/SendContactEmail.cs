using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

public sealed class SendContactEmail
{
    private readonly ILogger _logger;
    private readonly ISendGridClient _sendGrid;
    private readonly string _toAddress;

    public SendContactEmail(ILoggerFactory loggerFactory)
    {
        _logger     = loggerFactory.CreateLogger<SendContactEmail>();
        var apiKey  = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
        _toAddress  = Environment.GetEnvironmentVariable("TO_EMAIL") ?? "owner@example.com";
        _sendGrid   = new SendGridClient(apiKey);
    }

    [Function("SendContactEmail")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "contact")] HttpRequestData req)
    {
        var dto = await JsonSerializer.DeserializeAsync<ContactDto>(req.Body);

        if (dto is null || string.IsNullOrWhiteSpace(dto.Email))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("{\"error\":\"Invalid payload\"}");
            return bad;
        }

        var msg = MailHelper.CreateSingleEmail(
            new EmailAddress("noreply@yourdomain.com", "Website"),
            new EmailAddress(_toAddress),
            $"New enquiry from {dto.Name}",
            $"Name: {dto.Name}\nEmail: {dto.Email}\n\n{dto.Message}",
            $"<p><strong>Name:</strong> {dto.Name}<br/><strong>Email:</strong> {dto.Email}</p><p>{dto.Message}</p>");

        msg.ReplyTo = new EmailAddress(dto.Email, dto.Name);

        var sgResponse = await _sendGrid.SendEmailAsync(msg);
        _logger.LogInformation("SendGrid response: {Status}", sgResponse.StatusCode);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync("{\"success\":true}");
        return ok;
    }
}
