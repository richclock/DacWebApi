namespace DacWebApi
{
    public class ApiResponse
    {
        public bool IsSuccess { get; set; } = false;
        public string Msg { get; set; } = "";
        public object Data { get; set; } = null;

        public static ApiResponse Create(object data = null, bool isSuccess = true, string msg = null)
        {
            ApiResponse response = new ApiResponse();

            response.IsSuccess = isSuccess;
            response.Msg = msg;
            response.Data = data;
            return response;
        }
    }
}
