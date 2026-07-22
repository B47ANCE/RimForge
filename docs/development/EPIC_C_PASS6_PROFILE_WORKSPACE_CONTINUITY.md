# Epic C Pass 6 Profile Workspace Continuity

Pass 6 extends the canonical profile catalog with the user's last selected profile and library scope.

Profile selection and the full-library/active-profile toggle are saved through the same normalized, atomic `ProfileCatalogState.json` store introduced in Pass 4. On load, RimForge restores the remembered profile by case-insensitive name. If it no longer exists, the established editable-profile/first-profile fallback is selected and immediately becomes the new persisted value.

Rename updates the remembered name as part of the existing catalog metadata transition. Deletion clears the selection before persistence. Legacy catalog migration defaults to full-library scope and no remembered selection, preserving prior startup behavior.
