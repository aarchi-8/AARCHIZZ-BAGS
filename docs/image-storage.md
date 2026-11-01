# AARCHIZZ image storage

Local development stores product uploads outside the source tree. Set `AARCHIZZ_UPLOAD_ROOT` to choose an alternate writable directory; otherwise the application uses the current user's local application-data folder. Files are served under `/uploads` and their URL is stored in `ProductImages.UrlOrStorageKey`.

Local filesystem storage is not assumed to be durable on every hosting platform. Before deployment, replace `IProductImageStorage` with persistent cloud object storage and migrate existing storage keys as needed.