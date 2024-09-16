namespace Document_library.Services
{
    public class ServiceResult<T>
    {
        public bool Succeeded { get; set; }
        public T? Data { get; set; }
        public int Status { get; set; }
        public string? Message { get; set; }
        public string[] Errors { get; set; }

        public static ServiceResult<T> Success(T data) => new() { Succeeded = true, Status = StatusCodes.Status200OK, Data = data};
        public static ServiceResult<T> Failed(params string[] errors) => new() { Succeeded = false, Status = StatusCodes.Status400BadRequest, Errors = errors};
        public ServiceResult() => Errors = [];
    }

    public class ServiceResult
    {
        public bool Succeeded { get; set; }
        public string[] Errors { get; set; }
        public int Status { get; set; }
        public string? Message { get; set; }
        public static ServiceResult Success() => new() { Succeeded = true, Status = StatusCodes.Status200OK};
        public static ServiceResult Failed(params string[] errors) => new() { Succeeded = false, Status = StatusCodes.Status400BadRequest, Errors = errors };
        public ServiceResult() => Errors = [];
    }
    public static class ServiceResultExtensions
    {
        public static ServiceResult<T> WithMessage<T>(this ServiceResult<T> result, string message)
        {
            result.Message = message;
            return result;
        }
        public static ServiceResult<T> WithStatusCode<T>(this ServiceResult<T> result, int code)
        {
            result.Status = code;
            return result;
        }

        public static ServiceResult WithMessage(this ServiceResult result, string message)
        {
            result.Message = message;
            return result;
        }
        public static ServiceResult WithStatusCode(this ServiceResult result, int code)
        {
            result.Status = code;
            return result;
        }
    }
}
