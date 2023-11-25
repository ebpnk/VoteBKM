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
  "BanCommand": "mm_ban #{0} {1} VoteBan",//{0} - userid/nik, {1} - время, VoteBan - причина
  "MuteCommand": "mm_ban #{0} {1} VoteЬMute",//{0} - userid/nik, {1} - время, VoteBan - причина
  "KickCommand": "mm_kick #{0}",//{0} - userid/nik
  "BanDuration": 10,//Время в секундах
  "RequiredMajority": 0.5,//Процент голосов за бан,50% - 0.5
  "BanByUserId": true,// true - userid, false- nik
  "MuteByUserId": true,// true - userid, false- nik
  "KickByUserId": true// true - userid, false- nik
}
```
`Immunity`
`Admin immunity - @css/votebkm`


![image](https://github.com/ebpnk/VoteBKM/assets/49415003/ccfb929c-c22e-422d-b0c8-54dbda3b6c1d)

I recommend using this plugin [cs2-bans](https://github.com/Pisex/cs2-bans)