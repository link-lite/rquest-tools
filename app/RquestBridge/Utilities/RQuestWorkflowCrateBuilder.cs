using RquestBridge.Contracts;
using ROCrates.Models;
using System.Globalization;
using RquestBridge.Config;
using Flurl;
using ROCrates;
using System.Text.Json;
using RquestBridge.Constants;
namespace RquestBridge.Utilities;

public class RQuestWorkflowCrateBuilder : IROCrateBuilder
{
  private readonly WorkflowOptions _workflowOptions;
  private readonly CratePublishingOptions _publishingOptions;
  private ROCrate _crate = new ROCrate();

  public RQuestWorkflowCrateBuilder(WorkflowOptions workflowOptions, CratePublishingOptions publishingOptions)
  {
    _workflowOptions = workflowOptions;
    _publishingOptions = publishingOptions;

    // Add 5 Safes props to RootDataset
    UpdateRootDataset();
  }

  /// <summary>
  /// <para>Add the <c>CreateAction</c> to the RO-Crate.</para>
  /// <para>This includes inputs necessary to run the <c>rquest-omop-worker</c>.</para>
  /// </summary>
  /// <param name="queryFileName">The name of the file where the RQuest query is saved.</param>
  /// <param name="isAvailability">Is the query an availability query? If not, treat as a distribution query.</param>
  /// <param name="dbHost">The host address of the OMOP database.</param>
  /// <param name="dbName">The name of the database on the host.</param>
  /// <param name="dbUser">The user with access to the database.</param>
  /// <param name="dbPassword">The user's password.</param>
  public void AddCreateAction(string queryFileName, bool isAvailability, string dbHost, string dbName, string dbUser, string dbPassword)
  {
    var createActionId = $"#query-{Guid.NewGuid()}";
    var createAction = new ContextEntity(_crate, createActionId);
    createAction.SetProperty("@type", "CreateAction");
    createAction.SetProperty("actionStatus", ActionStatus.PotentialActionStatus);

    _crate.Entities.TryGetValue(GetWorkflowUrl(), out var workflow);
    if (workflow is not null) createAction.SetProperty("instrument", new Part { Id = workflow.Id });
    createAction.SetProperty("name", "RQuest Query");

    // set up OMOP worker inputs

    // body
    var body = AddQueryJsonMetadata(queryFileName);
    createAction.AppendTo("object", body);

    // is_availability
    var isAvailabilityEntity = AddQueryTypeMetadata(isAvailability);
    createAction.AppendTo("object", isAvailabilityEntity);

    // db_host
    var dbHostEntity = AddDbHostMetadata(dbHost);
    createAction.AppendTo("object", dbHostEntity);

    // db_name
    var dbNameEntity = AddDbNameMetadata(dbName);
    createAction.AppendTo("object", dbNameEntity);

    // db_user
    var dbUserEntity = AddDbUserMetadata(dbUser);
    createAction.AppendTo("object", dbUserEntity);

    // db_password
    var dbPasswordEntity = AddDbPasswordMetadata(dbPassword);
    createAction.AppendTo("object", dbPasswordEntity);

    _crate.Add(createAction);
  }

  public void AddLicense()
  {
    if (string.IsNullOrEmpty(_publishingOptions.License?.Uri)) return;

    var licenseProps = _publishingOptions.License.Properties;
    var licenseEntity = new CreativeWork(
      identifier: _publishingOptions.License.Uri,
      properties: JsonSerializer.SerializeToNode(licenseProps)?.AsObject());

    // Bug in ROCrates.Net: CreativeWork class uses the base constructor so @type is Thing by default
    licenseEntity.SetProperty("@type", "CreativeWork");

    _crate.Add(licenseEntity);

    _crate.RootDataset.SetProperty("license", new Part { Id = licenseEntity.Id });
  }

  public void AddProfile()
  {
    var profileEntity = new Entity(identifier: "https://w3id.org/trusted-wfrun-crate/0.3");
    profileEntity.SetProperty("@type", "Profile");
    profileEntity.SetProperty("name", "Trusted Workflow Run Crate profile");
    _crate.Add(profileEntity);
  }

  public void AddMainEntity()
  {
    var workflowURI = GetWorkflowUrl();
    var workflowEntity = new Dataset(identifier: workflowURI);
    workflowEntity.SetProperty("name", _workflowOptions.Name);
    _crate.Entities.TryGetValue("https://w3id.org/trusted-wfrun-crate/0.3", out var profile);

    if (profile is not null)
    {
      workflowEntity.SetProperty("conformsTo", new Part
      {
        Id = profile.Id
      });
    }
    workflowEntity.SetProperty("distribution", new Part
    {
      Id = Url.Combine(_workflowOptions.BaseUrl, _workflowOptions.Id.ToString(), "ro_crate").SetQueryParam("version", _workflowOptions.Version.ToString())
    });
    _crate.Add(workflowEntity);
    _crate.RootDataset.SetProperty("mainEntity", workflowEntity.Id);
  }

  private ROCrates.Models.File AddQueryJsonMetadata(string queryFileName)
  {
    var bodyParam = new ContextEntity(null, "#{_workflowOptions.Name}-inputs-body");
    bodyParam.SetProperty("@type", "FormalParameter");
    bodyParam.SetProperty("name", "body");
    bodyParam.SetProperty("dct:conformsTo", "https://bioschemas.org/profiles/FormalParameter/1.0-RELEASE/");
    var bodyEntity = new ROCrates.Models.File(null, queryFileName);
    bodyEntity.SetProperty("name", "rquest-query");
    bodyEntity.SetProperty("exampleOfWork", new Part { Id = bodyParam.Id });

    _crate.Add(bodyParam, bodyEntity);
    return bodyEntity;
  }

  private ContextEntity AddQueryTypeMetadata(bool isAvailability)
  {
    var paramId = "#{_workflowOptions.Name}-inputs-{0}";
    var entityId = "#input_{0}";

    var isAvailabilityParam = new ContextEntity(null, string.Format(paramId, isAvailability ? "is_availability" : "is_distribution"));
    isAvailabilityParam.SetProperty("@type", "FormalParameter");
    isAvailabilityParam.SetProperty("name", isAvailability ? "is_availability" : "is_distribution");
    isAvailabilityParam.SetProperty("dct:conformsTo", "https://bioschemas.org/profiles/FormalParameter/1.0-RELEASE/");
    var isAvailabilityEntity = new ContextEntity(null, string.Format(entityId, isAvailability ? "is_availability" : "is_distribution"));
    isAvailabilityEntity.SetProperty("@type", "PropertyValue");
    isAvailabilityEntity.SetProperty("name", isAvailability ? "is_availability" : "is_distribution");
    isAvailabilityEntity.SetProperty("value", isAvailability);
    isAvailabilityEntity.SetProperty("exampleOfWork", new Part { Id = isAvailabilityParam.Id });

    _crate.Add(isAvailabilityParam, isAvailabilityEntity);
    return isAvailabilityEntity;
  }

  private ContextEntity AddDbHostMetadata(string dbHost)
  {
    var dbHostParam = new ContextEntity(null, "#{_workflowOptions.Name}-inputs-db_host");
    dbHostParam.SetProperty("@type", "FormalParameter");
    dbHostParam.SetProperty("name", "db_host");
    dbHostParam.SetProperty("dct:conformsTo", "https://bioschemas.org/profiles/FormalParameter/1.0-RELEASE/");
    var dbHostEntity = new ContextEntity(null, "#input_db_host");
    dbHostEntity.SetProperty("@type", "PropertyValue");
    dbHostEntity.SetProperty("name", "db_host");
    dbHostEntity.SetProperty("value", dbHost);
    dbHostEntity.SetProperty("exampleOfWork", new Part { Id = dbHostParam.Id });

    _crate.Add(dbHostParam, dbHostEntity);
    return dbHostEntity;
  }

  private ContextEntity AddDbNameMetadata(string dbName)
  {
    var dbNameParam = new ContextEntity(null, "#{_workflowOptions.Name}-inputs-db_name");
    dbNameParam.SetProperty("@type", "FormalParameter");
    dbNameParam.SetProperty("name", "db_name");
    dbNameParam.SetProperty("dct:conformsTo", "https://bioschemas.org/profiles/FormalParameter/1.0-RELEASE/");
    var dbNameEntity = new ContextEntity(null, "#input_db_name");
    dbNameEntity.SetProperty("@type", "PropertyValue");
    dbNameEntity.SetProperty("name", "db_name");
    dbNameEntity.SetProperty("value", dbName);
    dbNameEntity.SetProperty("exampleOfWork", new Part { Id = dbNameParam.Id });

    _crate.Add(dbNameParam, dbNameEntity);
    return dbNameEntity;
  }

  private ContextEntity AddDbUserMetadata(string dbUser)
  {
    var dbUserParam = new ContextEntity(null, $"#{_workflowOptions.Name}-inputs-db_user");
    dbUserParam.SetProperty("@type", "FormalParameter");
    dbUserParam.SetProperty("name", "db_user");
    dbUserParam.SetProperty("dct:conformsTo", "https://bioschemas.org/profiles/FormalParameter/1.0-RELEASE/");
    var dbUserEntity = new ContextEntity(null, "#input_db_user");
    dbUserEntity.SetProperty("@type", "PropertyValue");
    dbUserEntity.SetProperty("name", "db_user");
    dbUserEntity.SetProperty("value", dbUser);
    dbUserEntity.SetProperty("exampleOfWork", new Part { Id = dbUserParam.Id });

    _crate.Add(dbUserParam, dbUserEntity);
    return dbUserEntity;
  }

  private ContextEntity AddDbPasswordMetadata(string dbPassword)
  {
    var dbPasswordParam = new ContextEntity(null, $"#{_workflowOptions.Name}-inputs-db_password");
    dbPasswordParam.SetProperty("@type", "FormalParameter");
    dbPasswordParam.SetProperty("name", "db_password");
    dbPasswordParam.SetProperty("dct:conformsTo", "https://bioschemas.org/profiles/FormalParameter/1.0-RELEASE/");
    var dbPasswordEntity = new ContextEntity(null, "#input_db_password");
    dbPasswordEntity.SetProperty("@type", "PropertyValue");
    dbPasswordEntity.SetProperty("name", "db_password");
    dbPasswordEntity.SetProperty("value", dbPassword);
    dbPasswordEntity.SetProperty("exampleOfWork", new Part { Id = dbPasswordParam.Id });

    _crate.Add(dbPasswordParam, dbPasswordEntity);
    return dbPasswordEntity;
  }

  public ROCrate GetROCrate()
  {
    ROCrate result = _crate;
    ResetCrate();
    return result;
  }

  public void ResetCrate()
  {
    _crate = new ROCrate();
    UpdateRootDataset();
  }

  private string GetWorkflowUrl()
  {
    return Url.Combine(_workflowOptions.BaseUrl, _workflowOptions.Id.ToString()).SetQueryParam("version", _workflowOptions.Version.ToString());
  }

  private void UpdateRootDataset()
  {
    _crate.RootDataset.SetProperty("conformsTo", new Part
    {
      Id = "https://w3id.org/trusted-wfrun-crate/0.3",
    });
    _crate.RootDataset.SetProperty("datePublished", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
  }

  public void AddAgent()
  {
    throw new NotImplementedException();
  }

  public void AddProject()
  {
    throw new NotImplementedException();
  }

  public void AddOrganisation()
  {
    throw new NotImplementedException();
  }
}