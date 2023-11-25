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
  "BanCommand": "mm_ban #{0} {1} VoteBan",//{0} - userid/nik, {1} - time, VoteBan - reason
  "MuteCommand": "mm_ban #{0} {1} VoteЬMute",//{0} - userid/nik, {1} - time, VoteBan - reason
  "KickCommand": "mm_kick #{0}",//{0} - userid/nik
  "BanDuration": 10,//Time in seconds
  "RequiredMajority": 0.5,//Percentage of votes,50% - 0.5
  "BanByUserId": true,// true - userid, false- nik
  "MuteByUserId": true,// true - userid, false- nik
  "KickByUserId": true// true - userid, false- nik
  "MinimumPlayersToStartVote": 4 // The beginning of voting depends on the number of players
}
```
`Immunity:`

`Admin immunity - @css/votebkm`


![image](https://github.com/ebpnk/VoteBKM/assets/49415003/ccfb929c-c22e-422d-b0c8-54dbda3b6c1d)

I recommend using this plugin [cs2-bans](https://github.com/Pisex/cs2-bans)
