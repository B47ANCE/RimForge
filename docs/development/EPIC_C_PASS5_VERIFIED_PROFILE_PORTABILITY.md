# Epic C Pass 5 Verified Profile Portability

Pass 5 adds a read-only inspection boundary before a portable profile package can enter the profile workspace.

`IProfilePackageInspectionService` verifies that an archive contains exactly one manifest and one declared ModsConfig entry, enforces conservative size limits and a safe flat config name, validates the SHA-256 checksum, parses the XML, and confirms that manifest and config load orders match.

After integrity validation, the service compares package IDs and the target RimWorld version against the installed library. The main client rejects invalid packages before asking for an import name. Valid packages with missing or version-incompatible mods require explicit confirmation and show the warning counts.

The existing restore path remains responsible for atomic profile creation; inspection is deliberately read-only and cannot mutate profile or RimWorld state.
