using System.Net;

namespace FieldEventManagement.Agent.Models;

public class BackendResponseDto
{
    public bool IsSuccess { get; set; }
    public HttpStatusCode StatusCode { get; set; }
}