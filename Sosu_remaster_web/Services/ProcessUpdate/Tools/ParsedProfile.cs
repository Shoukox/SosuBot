using Sosu.osu.V1.Enums;
using Sosu.Types;
using User = Sosu.osu.V1.Types.User;

namespace Sosu.Services.ProcessUpdate.Tools
{
    public class ParsedProfile
    {
        public int userId;
        public string? userName;

        public string different = "";
        public IEnumerable<osuUser>? osuUserLastVersion;

        public ParsedProfile(int userId)
        {
            this.userId = userId;
        }
        public ParsedProfile(string userName)
        {
            this.userName = userName;
        }
        public async Task<User> Parse(GameMode mode = GameMode.Standard)
        {
            User osuUser = await Variables.osuApi.GetUserInfoByNameAsync(userName is null ? $"{userId}" : userName, (int)mode, userName is null ? "id" : "string");
            if (osuUser == null)
            {
                return null;
            }

            if ((int)mode == 0)
            {
                this.osuUserLastVersion = Variables.osuUsers.Where(m => m.osuName == osuUser.username());
                if (osuUserLastVersion != null && osuUserLastVersion.Count() != 0)
                {
                    double differentNumber = Math.Round(double.Parse(osuUser.pp_raw()) - osuUserLastVersion.OrderByDescending(m => m.pp).First().pp, 2);
                    char sign = differentNumber >= 0 ? '+' : '-';
                    this.different = $"({sign}{differentNumber})";
                }
            }
            return osuUser;
        }
    }
}
