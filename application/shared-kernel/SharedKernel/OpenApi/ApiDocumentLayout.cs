namespace SharedKernel.OpenApi;

public enum ApiDocumentLayout
{
    // One unfiltered OpenAPI document. Endpoints with no WithGroupName are included.
    Single,

    // Two filtered documents: 'account' and 'back-office'. Each endpoint group must declare WithGroupName.
    AccountAndBackOffice
}
