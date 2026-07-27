# Security and family privacy

MineDeck and Bridge bind to loopback by default and authenticate every mutation. The Minecraft whitelist and online authentication stay enabled. Remote parent access uses Tailscale; V1 opens no router port.

Never commit worlds, player names/UUIDs, IP addresses, chat/activity history, exploration masks, credentials, access tokens, backups, Ledger databases, or generated map tiles. Report security issues privately to the repository owner rather than opening an issue containing family data.

The GitHub repository is source and reconstruction metadata only. Production deploys require local secrets, a verified independent backup, and owner review.
