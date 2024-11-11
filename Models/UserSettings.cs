using System.Text.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ImageGeneratorTgBot.Models;

[Table("UserSettings")]
public class UserSettings : BaseModel
{
	[PrimaryKey("id", false)]
	public int Id { get; set; }

	[Column("created_at", ignoreOnInsert: true)]
	public string CreatedAt { get; set; }

	[Column("themes")]
	public JsonElement Themes { get; set; }

	[Column("function")]
	public string Function { get; set; }

	[Column("user_id")]
	public int UserId { get; set; }

	[Column("time_to_send")]
	public string TimeToSend { get; set; }
}