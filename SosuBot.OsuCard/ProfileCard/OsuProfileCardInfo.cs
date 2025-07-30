using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Graphics.ProfileCard
{
    public record OsuProfileCardInfo (
        string Username, 
        double PP, 
        double Accuracy,
        string AvatarUrl);
}
