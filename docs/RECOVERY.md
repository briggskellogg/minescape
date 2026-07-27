# Recovery

1. Stop MineScape through MineDeck or wait for its confirmed clean stop.
2. Select a verified backup epoch and restore it into MineJammer first.
3. Boot MineJammer, verify dimensions, player/heart state, wards, loot provenance, fog history, databases, and manifest hashes.
4. Create a new pre-restore recovery point for the current production state.
5. Promote the verified complete epoch transactionally. Partial region-file recovery is prohibited unless the parcel tool proves matching `region`, `entities`, and `poi` coverage.
6. Boot and protocol-ping MineScape; reconcile ambiguous reward grants and character events before admitting players.

Never use GitHub as a world backup. Keep at least one verified copy on a physically separate device; encrypted off-site backup is recommended.
