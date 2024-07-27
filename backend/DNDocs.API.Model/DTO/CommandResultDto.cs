using DNDocs.API.Model.DTO.Enum;

namespace DNDocs.API.Model.DTO
{
    public class CommandResultDto : HandlerResultDto
    {
        public CommandResultDto(bool success, string errorMessage, IEnumerable<FieldErrorDto> fieldErrors) : base(success, errorMessage, fieldErrors)
        {
        }
    }

    public class CommandResultDto<TResult> : HandlerResultDto
    {
        public TResult Result { get; set; }

        public CommandResultDto(TResult result, bool success, string errorMessage, IEnumerable<FieldErrorDto> fieldErrors)
            : base(success, errorMessage, fieldErrors)
        {
            Result = result;
        }
    }

    public class FieldErrorDto
    {
        public string FieldName { get; set; }
        public string ErrorMessage { get; set; }

        public FieldErrorDto(string field, string error)
        {
            FieldName = field;
            ErrorMessage = error;
        }
    }
}
