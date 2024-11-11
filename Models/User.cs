using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ImageGeneratorTgBot.Models;

[Table("Users")]
public class User : BaseModel
{
	[PrimaryKey("id", false)]
	public int Id { get; set; }

	[Column("created_at", ignoreOnInsert: true)]
	public string CreatedAt { get; set; }

	[Column("chat_id")]
	public string ChatId { get; set; }
}