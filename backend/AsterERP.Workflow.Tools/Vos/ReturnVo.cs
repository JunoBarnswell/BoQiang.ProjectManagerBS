using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Tools.Vos
{
    public class ReturnVo<T>
    {
        public string Code { get; set; }
        public string Msg { get; set; }
        public T Data { get; set; }

        public bool IsSuccess()
        {
            if (!string.IsNullOrWhiteSpace(Code) && ReturnCode.SUCCESS.Equals(Code))
            {
                return true;
            }
            return false;
        }

        public ReturnVo() { }

        public ReturnVo(string code, string msg)
        {
            Code = code;
            Msg = msg;
        }

        public ReturnVo(string code, string msg, T data)
        {
            Code = code;
            Msg = msg;
            Data = data;
        }
    }
}
