# Visualization migration

Visualization scenes and questions now run through the SpeedReading API while
keeping the existing `VisualizationScenes` and `VisualizationQuestions` tables.

## Canonical endpoints

- `GET /api/speed-reading/visualization/exercises/{exerciseId}/scenes`
- `GET /api/speed-reading/visualization/scenes/{sceneId}`
- `GET /api/speed-reading/visualization/scenes/difficulty/{difficultyLevel}`
- `GET|POST|PUT|DELETE /api/speed-reading/admin/visualization-scenes`
- `GET /api/speed-reading/admin/visualization-scenes/exercises`
- `POST /api/speed-reading/admin/visualization-scenes/import/csv`

The old `/api/v1/visualization` and
`/api/v1/admin/visualization-scenes` paths remain available through Caddy.
Scene writes validate and persist questions by serializing the frontend's
`options` array into the legacy `OptionsJson` column. Updates soft-delete the
previous question set before inserting the submitted set.

CSV imports require `ExerciseId`, `Description`, and may contain `Q1..Q5`,
`A1..A5`, `O1..O5` (pipe-separated options), `T1..T5`, and `H1..H5` columns.

The source project did not provide visualization PDF/DOCX export endpoints;
the frontend's 404-producing export helpers were removed instead of being
represented by fake files.
