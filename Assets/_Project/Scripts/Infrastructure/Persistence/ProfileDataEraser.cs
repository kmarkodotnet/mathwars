using System;

// local-persistence skill: "Profil-/adattörlés: egyetlen, visszavonhatatlan,
// megerősítést kérő művelet törli az ÖSSZES, az adott profilhoz tartozó
// fájlt." (ADR-011 6.)
namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// The one place in the codebase allowed to delete a player's saved
    /// data. Deletes BOTH the profile and statistics files in one call —
    /// there is no partial/selective deletion, by design, so a caller can
    /// never "accidentally" leave orphaned statistics behind while clearing
    /// the profile (or vice versa).
    /// </summary>
    public sealed class ProfileDataEraser
    {
        private readonly ILocalSaveRepository _saveRepository;
        private readonly ILocalStatisticsRepository _statisticsRepository;

        public ProfileDataEraser(ILocalSaveRepository saveRepository, ILocalStatisticsRepository statisticsRepository)
        {
            _saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            _statisticsRepository = statisticsRepository ?? throw new ArgumentNullException(nameof(statisticsRepository));
        }

        /// <summary>
        /// Deletes every save file for this profile. Irreversible —
        /// `confirmed` must be explicitly true; the caller (Presentation
        /// layer) is responsible for having already shown a confirmation
        /// dialog to the parent/player before reaching this call.
        /// </summary>
        public void Erase(bool confirmed)
        {
            if (!confirmed)
            {
                throw new InvalidOperationException(
                    "A profiltörlés visszavonhatatlan — csak explicit megerősítéssel (confirmed: true) hívható.");
            }

            _saveRepository.Delete();
            _statisticsRepository.Delete();
        }
    }
}
