# VoteBKM
Plugin for blocking players by voting

The plugin has not been tested in a large environment


`Commands:`
```
!voteban
!votemute
!votekick
!votereset - admin flag "@css/votebkm"

```

`voteban_config.json`
```
{
  //"BanCommand": "mm_ban #{0} {1} VoteBan",//{0} - userid/nik, {1} - time, VoteBan - reason // Removed from the code
  "MuteCommand": "mm_mute #{0} {1} VoteMute",//{0} - userid/nik, {1} - time, VoteBan - reason
  "KickCommand": "mm_kick #{0}",//{0} - userid/nik
  "BanDuration": 10,//Time in seconds
  "RequiredMajority": 0.5,//Percentage of votes,50% - 0.5
  "BanByUserId": true,// true - userid, false- nik //Don't change these lines
  "MuteByUserId": true,// true - userid, false- nik //Don't change these lines
  "KickByUserId": true// true - userid, false- nik //Don't change these lines
  "MinimumPlayersToStartVote": 4 // The beginning of voting depends on the number of players
}
```
```
BannedPlayersConfig.json // Bans are stored here // Automatically deleted ban
```

`Immunity:`

`Admin immunity - @css/votebkm`


![image](https://github.com/ebpnk/VoteBKM/assets/49415003/92a84044-d2d2-4d52-8a25-83563533a189)



I recommend using this plugin [cs2-bans](https://github.com/Pisex/cs2-bans)
