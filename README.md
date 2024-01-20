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
  "BanDuration": 3600,//Time in seconds
  "RequiredMajority": 0.5,//Percentage of votes,50% - 0.5
  "MinimumPlayersToStartVote": 4 // The beginning of voting depends on the number of players
}
```
```
BannedPlayersConfig.json // Bans are stored here // Automatically deleted ban
```

`Immunity:`

`Admin immunity - @css/votebkm`


![image](https://github.com/ebpnk/VoteBKM/assets/49415003/92a84044-d2d2-4d52-8a25-83563533a189)



