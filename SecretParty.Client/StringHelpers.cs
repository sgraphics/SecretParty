namespace SecretParty.Client
{
	public static class StringExtensions
	{
		public static string Sanitize(this string obj)
		{
			return obj?.Replace("'", "");
		}
	}
}
