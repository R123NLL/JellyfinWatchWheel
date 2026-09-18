# Watch Wheel: episode progress update

Replace these five files inside the existing plugin project, preserving the Models/ and Services/ paths. Back up or commit your current files first. No controller or service-registration changes are needed.

- Models/TvSeriesProgress.cs
- Models/WatchWheelItem.cs
- Services/TvSeriesService.cs
- Services/CandidateService.cs
- Services/FilterService.cs

## Behavior

The next episode is the first unwatched regular episode in season/episode order. A partially watched episode resumes when it is that selected episode; a later partially watched episode does not jump ahead of earlier unwatched episodes. Unknown season/episode numbers sort last. Season zero specials are excluded from selection, remaining counts, and started-state calculation. A specials-only series therefore does not appear.

An unfinished series is in progress if any regular episode has been played or has positive playback progress. Turning IncludeInProgress off excludes that entire series, including when one episode is finished and the next has not been started. Movies retain their resume-position-based behavior.

WatchWheelItem.Id remains the movie/series ID for artwork and details. NextEpisodeId is the playback target for series. PlaybackPositionTicks now carries that selected episode's resume position for series, or the movie position for movies. IsInProgress is the explicit filter state; a series can have IsInProgress=true with PlaybackPositionTicks=0.

Filter options derive from all eligible candidates with IncludeInProgress=true. They are global options, not dependent on the currently selected type, genre, decade, or in-progress toggle. Types retains the existing fixed list.

## Build and verify

From your existing solution directory:

```powershell
dotnet build .\Jellyfin.Plugin.Template\Jellyfin.Plugin.Template.csproj -c Release
```

This bundle was reviewed against the pasted source, but could not be compiled here: the complete project and .NET SDK are unavailable. It preserves the referenced WatchWheelFilters and WatchWheelResult contracts inferred from the pasted controller and services.

After building, deploy using your established plugin replacement process, restart Jellyfin, and check the authenticated Items endpoint:

| Scenario | Expected |
| --- | --- |
| All regular episodes unplayed, no progress | Series appears; IsInProgress=false; first regular episode selected |
| S1E1 partially watched | IsInProgress=true; S1E1 selected with its resume ticks |
| S1E1 played, S1E2 untouched | IsInProgress=true; S1E2 selected with zero resume ticks |
| Either started-series case, IncludeInProgress=false | Entire series excluded |
| All regular episodes played, specials unplayed | Series excluded |
| Empty or specials-only series | Series excluded |
| Partially watched movie, IncludeInProgress=false | Movie excluded |
| Earlier unwatched episode and later resumed episode | Earlier episode selected; later resume position is not reused |

The frontend Play action has not been modified. It must use NextEpisodeId for a series, Id for a movie, and PlaybackPositionTicks from that same candidate. Details and posters continue using Id. The existing per-series episode queries remain; the Filters endpoint now also uses them, so large libraries may need later performance work. Missing/virtual media handling has not been added by this update.
