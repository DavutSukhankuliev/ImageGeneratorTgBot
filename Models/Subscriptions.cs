using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ImageGeneratorTgBot.Models;

[Table("Supscriptions")]
public class Supscription : BaseModel
{
	[PrimaryKey("id", false)]
	public int Id { get; set; }

	[Column("created_at", ignoreOnInsert: true)]
	public string CreatedAt { get; set; }

	[Column("expiration_date")]
	public string? ExpirationDate { get; set; }

	[Column("user_id")]
	public int UserId { get; set; }
}