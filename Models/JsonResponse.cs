namespace HealthCoverage.Models
{
    public class JsonResponse
    {
        public List<HeadModel> Head { get; set; }
        public List<ResultModel> Result { get; set; }
		public string? Interpreted { get; set; }
	}

    public class HeadModel
    {
        public string ProgramCode { get; set; }
        public string ProgramName { get; set; }
    }
	public class ResultModel
    {
        public string? ProgramCode { get; set; }
        public string? TestCode { get; set; }
        public string? TestName { get; set; }
        public string? Result { get; set; }
		public string? Flag { get; set; }
		public string? NormalRang { get; set; }
        public string? Unit { get; set; }
		public string? ResultStyle { get; set; }
	}
	public class InterpreteModel
	{
		public string? Text { get; set; }
	}
}
