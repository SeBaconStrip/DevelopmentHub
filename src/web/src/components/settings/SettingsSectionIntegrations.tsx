import githubIcon from "../../assets/icons/github.svg";
import azureDevOpsIcon from "../../assets/icons/azure-devops.svg";
import type { AppConfig, PullRequestProvider } from "../../types";
import { Section, Field, InfoTooltip } from "./SettingsHelpers";

type ProviderField = {
  key: string;
  label: string;
  placeholder: string;
  type?: "text" | "password";
  hint?: string;
  tooltip?: React.ReactNode;
};

type ProviderOption = {
  id: PullRequestProvider;
  label: string;
  description: string;
  icon: string;
  sectionTitle: string;
  sectionHint?: string;
  fields: ProviderField[];
};

const PULL_REQUEST_PROVIDER_OPTIONS: {
  [K in PullRequestProvider]: ProviderOption;
} = {
  azureDevOps: {
    id: "azureDevOps",
    label: "Azure DevOps",
    description: "Use Azure DevOps repositories and pull requests.",
    icon: azureDevOpsIcon,
    sectionTitle: "Azure DevOps",
    fields: [
      {
        key: "organization",
        label: "Organization",
        placeholder: "myorg",
        tooltip: (
          <>
            <p>Your Azure DevOps organization name as it appears in the URL:</p>
            <code>dev.azure.com/<strong>organization</strong></code>
            <p className="tooltip-used-by">Used by: Pull Requests · Workflows</p>
          </>
        ),
      },
      {
        key: "project",
        label: "Project",
        placeholder: "MyProject",
        tooltip: (
          <>
            <p>The Azure DevOps project to fetch pull requests from.</p>
            <p className="tooltip-used-by">Used by: Pull Requests · Workflows</p>
          </>
        ),
      },
      {
        key: "userEmail",
        label: "User Email",
        placeholder: "you@example.com",
        tooltip: (
          <>
            <p>Your Azure DevOps account email. Used to identify pull requests you authored or are reviewing.</p>
            <p className="tooltip-used-by">Used by: Pull Requests</p>
          </>
        ),
      },
      {
        key: "pat",
        label: "Personal Access Token",
        placeholder: "Leave blank to keep existing",
        type: "password",
        tooltip: (
          <>
            <p>Personal Access Token for Azure DevOps authentication.</p>
            <p><strong>Required scopes:</strong></p>
            <ul>
              <li><strong>Code – Read</strong> · pull requests, repository list</li>
              <li><strong>Build – Read</strong> · workflow pipeline artifact downloads</li>
              <li><strong>Profile – Read</strong> · user identity</li>
            </ul>
            <p className="tooltip-used-by">Used by: Pull Requests · Workflows</p>
          </>
        ),
      },
    ],
  },
  github: {
    id: "github",
    label: "GitHub",
    description: "Use GitHub pull requests and repositories.",
    icon: githubIcon,
    sectionTitle: "GitHub",
    sectionHint:
      "GitHub pull requests are loaded via search for open pull requests that involve the configured user. You can optionally add extra search qualifiers to narrow the result set.",
    fields: [
      {
        key: "userLogin",
        label: "User Login",
        placeholder: "your-login",
        tooltip: (
          <>
            <p>Your GitHub username. Used to search for open PRs where you are author, reviewer, or mentioned.</p>
            <p className="tooltip-used-by">Used by: Pull Requests</p>
          </>
        ),
      },
      {
        key: "searchQuery",
        label: "Extra Search Query",
        placeholder: "org:my-org -label:wip",
        tooltip: (
          <>
            <p>Optional GitHub search qualifiers appended to the base PR query.</p>
            <p><strong>Examples:</strong></p>
            <ul>
              <li><code>org:my-org</code> · limit to an org</li>
              <li><code>repo:owner/name</code> · specific repo</li>
              <li><code>team-review-requested:org/team</code> · team reviews</li>
              <li><code>-label:wip</code> · exclude WIP PRs</li>
            </ul>
            <p className="tooltip-used-by">Used by: Pull Requests</p>
          </>
        ),
      },
      {
        key: "pat",
        label: "Personal Access Token",
        placeholder: "Leave blank to keep existing",
        type: "password",
        tooltip: (
          <>
            <p>Classic or fine-grained Personal Access Token for GitHub authentication.</p>
            <p><strong>Classic token scopes:</strong></p>
            <ul>
              <li><strong>repo</strong> · private repo access, PRs, releases</li>
            </ul>
            <p><strong>Fine-grained permissions:</strong></p>
            <ul>
              <li><strong>Pull requests – Read</strong> · PR list and details</li>
              <li><strong>Contents – Read</strong> · release asset downloads</li>
              <li><strong>Metadata – Read</strong> · required for all repos</li>
            </ul>
            <p className="tooltip-used-by">Used by: Pull Requests · Workflows</p>
          </>
        ),
      },
    ],
  },
};

const PULL_REQUEST_PROVIDER_LIST = [
  { ...PULL_REQUEST_PROVIDER_OPTIONS.azureDevOps },
  { ...PULL_REQUEST_PROVIDER_OPTIONS.github },
] satisfies ProviderOption[];

interface IntegrationsPageProps {
  form: AppConfig;
  setProviderField: (
    providerId: PullRequestProvider,
    key: string,
    value: string,
  ) => void;
}

export function SettingsSectionIntegrations({ form, setProviderField }: IntegrationsPageProps) {
  return (
    <>
      {PULL_REQUEST_PROVIDER_LIST.map((provider) => {
        const providerConfig = form.pullRequestProviders[provider.id] ?? {};

        return (
          <Section key={provider.id} title={provider.sectionTitle}>
            <div className="provider-section-heading">
              <img src={provider.icon} alt="" className="provider-radio-icon" />
              <span className="settings-page-hint">{provider.description}</span>
              {provider.sectionHint && (
                <InfoTooltip content={<p>{provider.sectionHint}</p>} />
              )}
            </div>
            {provider.fields.map((field) => (
              <Field
                key={field.key}
                label={field.label}
                tooltip={field.tooltip}
              >
                <input
                  type={field.type ?? "text"}
                  className="settings-input"
                  value={providerConfig[field.key] ?? ""}
                  onChange={(e) =>
                    setProviderField(provider.id, field.key, e.target.value)
                  }
                  placeholder={field.placeholder}
                />
                {field.hint && (
                  <span className="settings-field-hint">{field.hint}</span>
                )}
              </Field>
            ))}
          </Section>
        );
      })}
    </>
  );
}
