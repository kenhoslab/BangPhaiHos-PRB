using System;
using System.Collections.Generic;
using NodaTime;

namespace HealthCoverage.Models.db;

public partial class RegisterLab
{
	public long Id { get; set; } // BIGINT in PostgreSQL
	public string LabNumber { get; set; } = null!;
	public string? IdentityCard { get; set; }
	public string? Hn { get; set; }
	public string? FullName { get; set; }
	public string? Sex { get; set; }
	public string? AgeStr { get; set; }
	public string? BirthDate { get; set; }
	public string? Doctor { get; set; }
	public string? RegisterDate { get; set; } // VARCHAR(15)
	public string? ResultData { get; set; } // TEXT  

}
