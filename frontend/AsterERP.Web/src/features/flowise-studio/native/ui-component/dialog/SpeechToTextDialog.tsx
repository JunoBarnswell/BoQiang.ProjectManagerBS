import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import FormControl from '@mui/material/FormControl';
import FormHelperText from '@mui/material/FormHelperText';
import InputLabel from '@mui/material/InputLabel';
import ListItem from '@mui/material/ListItem';
import ListItemAvatar from '@mui/material/ListItemAvatar';
import ListItemText from '@mui/material/ListItemText';
import MenuItem from '@mui/material/MenuItem';
import Select, { type SelectChangeEvent } from '@mui/material/Select';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useEffect, useMemo, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceDto } from '../../../types/shared.types';
import assemblyAiIcon from '../../assets/images/assemblyai.png';
import azureIcon from '../../assets/images/azure_openai.svg';
import groqIcon from '../../assets/images/groq.png';
import localAiIcon from '../../assets/images/localai.png';
import openAiIcon from '../../assets/images/openai.svg';

type SpeechToTextProviderName = 'assemblyAiTranscribe' | 'azureCognitive' | 'groqWhisper' | 'localAISTT' | 'none' | 'openAIWhisper';
type SpeechToTextInputType = 'credential' | 'number' | 'options' | 'string';

interface SpeechToTextDialogProps {
  credentials: FlowiseResourceDto[];
  initialJson: string;
  open: boolean;
  saving: boolean;
  title: string;
  onClose: () => void;
  onConfirm: (json: string) => void;
}

interface SpeechToTextInputDefinition {
  credentialNames?: string[];
  defaultValue?: string;
  descriptionKey?: string;
  labelKey: string;
  name: string;
  optional?: boolean;
  options?: Array<{ labelKey: string; value: string }>;
  placeholder?: string;
  rows?: number;
  step?: number;
  type: SpeechToTextInputType;
}

interface SpeechToTextProviderDefinition {
  credentialNames: string[];
  icon: string;
  inputs: SpeechToTextInputDefinition[];
  labelKey: string;
  name: Exclude<SpeechToTextProviderName, 'none'>;
  url: string;
}

type SpeechToTextConfig = Record<string, Record<string, unknown>>;

const speechToTextProviders: Record<Exclude<SpeechToTextProviderName, 'none'>, SpeechToTextProviderDefinition> = {
  openAIWhisper: {
    credentialNames: ['openAIApi'],
    icon: openAiIcon,
    labelKey: flowiseI18nKeys.stt.openAiWhisper,
    name: 'openAIWhisper',
    url: 'https://platform.openai.com/docs/guides/speech-to-text',
    inputs: [
      credentialInput(['openAIApi']),
      textInput('language', flowiseI18nKeys.fields.language, flowiseI18nKeys.stt.descriptionLanguage, 'en', true),
      textInput('prompt', flowiseI18nKeys.fields.prompt, flowiseI18nKeys.stt.descriptionPrompt, undefined, true, 4),
      numberInput('temperature', flowiseI18nKeys.configuration.temperature, flowiseI18nKeys.stt.descriptionTemperature, true, 0.1)
    ]
  },
  assemblyAiTranscribe: {
    credentialNames: ['assemblyAIApi'],
    icon: assemblyAiIcon,
    labelKey: flowiseI18nKeys.stt.assemblyAi,
    name: 'assemblyAiTranscribe',
    url: 'https://www.assemblyai.com/',
    inputs: [credentialInput(['assemblyAIApi'])]
  },
  localAISTT: {
    credentialNames: ['localAIApi'],
    icon: localAiIcon,
    labelKey: flowiseI18nKeys.stt.localAi,
    name: 'localAISTT',
    url: 'https://localai.io/features/audio-to-text/',
    inputs: [
      credentialInput(['localAIApi']),
      textInput('baseUrl', flowiseI18nKeys.configuration.baseUrl, flowiseI18nKeys.stt.descriptionBaseUrl),
      textInput('language', flowiseI18nKeys.fields.language, flowiseI18nKeys.stt.descriptionLanguage, 'en', true),
      textInput('model', flowiseI18nKeys.configuration.model, flowiseI18nKeys.stt.descriptionModel, 'whisper-1', true),
      textInput('prompt', flowiseI18nKeys.fields.prompt, flowiseI18nKeys.stt.descriptionPrompt, undefined, true, 4),
      numberInput('temperature', flowiseI18nKeys.configuration.temperature, flowiseI18nKeys.stt.descriptionTemperature, true, 0.1)
    ]
  },
  azureCognitive: {
    credentialNames: ['azureCognitiveServices'],
    icon: azureIcon,
    labelKey: flowiseI18nKeys.stt.azureCognitive,
    name: 'azureCognitive',
    url: 'https://azure.microsoft.com/en-us/products/cognitive-services/speech-services',
    inputs: [
      credentialInput(['azureCognitiveServices']),
      textInput('language', flowiseI18nKeys.fields.language, flowiseI18nKeys.stt.descriptionLanguage, 'en-US', true),
      {
        defaultValue: 'Masked',
        descriptionKey: flowiseI18nKeys.stt.descriptionProfanity,
        labelKey: flowiseI18nKeys.configuration.profanityFilterMode,
        name: 'profanityFilterMode',
        optional: true,
        options: [
          { labelKey: flowiseI18nKeys.stt.optionNone, value: 'None' },
          { labelKey: flowiseI18nKeys.stt.optionMasked, value: 'Masked' },
          { labelKey: flowiseI18nKeys.stt.optionRemoved, value: 'Removed' }
        ],
        type: 'options'
      },
      textInput('channels', flowiseI18nKeys.configuration.audioChannels, flowiseI18nKeys.stt.descriptionAudioChannels, '0,1')
    ]
  },
  groqWhisper: {
    credentialNames: ['groqApi'],
    icon: groqIcon,
    labelKey: flowiseI18nKeys.stt.groqWhisper,
    name: 'groqWhisper',
    url: 'https://console.groq.com/',
    inputs: [
      textInput('model', flowiseI18nKeys.configuration.model, flowiseI18nKeys.stt.descriptionModel, 'whisper-large-v3', true),
      credentialInput(['groqApi']),
      textInput('language', flowiseI18nKeys.fields.language, flowiseI18nKeys.stt.descriptionLanguage, 'en', true),
      numberInput('temperature', flowiseI18nKeys.configuration.temperature, flowiseI18nKeys.stt.descriptionTemperature, true, 0.1)
    ]
  }
};

export function SpeechToTextDialog({ credentials, initialJson, open, saving, title, onClose, onConfirm }: SpeechToTextDialogProps) {
  const { translate } = useI18n();
  const [speechToText, setSpeechToText] = useState<SpeechToTextConfig>({});
  const [selectedProvider, setSelectedProvider] = useState<SpeechToTextProviderName>('none');
  const provider = selectedProvider === 'none' ? null : speechToTextProviders[selectedProvider];
  const credentialOptions = useMemo(() => filterCredentials(credentials, provider?.credentialNames ?? []), [credentials, provider]);
  const selectedProviderConfig = selectedProvider === 'none' ? {} : speechToText[selectedProvider] ?? {};
  const credentialRequired = selectedProvider !== 'none' && !selectedProviderConfig.credentialId;

  useEffect(() => {
    if (!open) {
      return;
    }

    const parsed = parseSpeechToTextConfig(initialJson);
    setSpeechToText(parsed);
    setSelectedProvider(resolveSelectedProvider(parsed));
  }, [initialJson, open]);

  const setProviderValue = (providerName: SpeechToTextProviderName, inputName: string, value: unknown) => {
    setSpeechToText((current) => {
      const next: SpeechToTextConfig = { ...current, [providerName]: { ...(current[providerName] ?? {}), [inputName]: value } };
      if (inputName === 'status' && value === true) {
        for (const key of Object.keys(speechToTextProviders)) {
          if (key !== providerName) {
            next[key] = { ...(current[key] ?? {}), status: false };
          }
        }
        if (providerName !== 'none') {
          next.none = { ...(current.none ?? {}), status: false };
        }
      }
      return next;
    });
  };

  const save = () => {
    if (credentialRequired) {
      return;
    }

    const nextConfig = markProviderActive(speechToText, selectedProvider);
    onConfirm(JSON.stringify(nextConfig));
  };

  return (
    <Dialog
      aria-describedby="flowise-speech-to-text-description"
      aria-labelledby="flowise-speech-to-text-title"
      fullWidth
      maxWidth="sm"
      open={open}
      onClose={onClose}
    >
      <DialogTitle id="flowise-speech-to-text-title" sx={{ fontSize: '1rem' }}>
        {title}
      </DialogTitle>
      <DialogContent id="flowise-speech-to-text-description">
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, mb: 1 }}>
          <Typography>{translate(flowiseI18nKeys.configuration.providers)}</Typography>
          <FormControl fullWidth>
            <Select size="small" value={selectedProvider} onChange={(event: SelectChangeEvent) => setSelectedProvider(event.target.value as SpeechToTextProviderName)}>
              <MenuItem value="none">{translate(flowiseI18nKeys.stt.none)}</MenuItem>
              {Object.values(speechToTextProviders).map((item) => (
                <MenuItem key={item.name} value={item.name}>
                  {translate(item.labelKey)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Box>
        {provider ? (
          <>
            <ListItem alignItems="center" sx={{ mt: 3 }}>
              <ListItemAvatar>
                <Box
                  sx={{
                    alignItems: 'center',
                    bgcolor: '#fff',
                    borderRadius: '50%',
                    display: 'flex',
                    flexShrink: 0,
                    height: 50,
                    justifyContent: 'center',
                    width: 50
                  }}
                >
                  <Box
                    alt={translate(provider.labelKey)}
                    component="img"
                    src={provider.icon}
                    sx={{
                      height: '100%',
                      objectFit: 'contain',
                      p: 1.25,
                      width: '100%'
                    }}
                  />
                </Box>
              </ListItemAvatar>
              <ListItemText
                primary={translate(provider.labelKey)}
                secondary={(
                  <a href={provider.url} rel="noreferrer" target="_blank">
                    {provider.url}
                  </a>
                )}
                sx={{ ml: 1 }}
              />
            </ListItem>
            {provider.inputs.map((input) => (
              <Box key={input.name} sx={{ p: 2 }}>
                <Typography>
                  {translate(input.labelKey)}
                  {!input.optional ? <Box component="span" sx={{ color: 'error.main' }}> *</Box> : null}
                </Typography>
                {input.descriptionKey ? (
                  <Typography color="text.secondary" sx={{ mb: 1 }} variant="body2">
                    {translate(input.descriptionKey)}
                  </Typography>
                ) : null}
                <SpeechToTextInput
                  credentialOptions={credentialOptions}
                  input={input}
                  value={selectedProviderConfig[input.name] ?? input.defaultValue ?? ''}
                  onChange={(value) => setProviderValue(provider.name, input.name, value)}
                />
              </Box>
            ))}
            {credentialRequired ? (
              <FormHelperText error sx={{ px: 2 }}>
                {translate(flowiseI18nKeys.messages.speechToTextCredentialRequired)}
              </FormHelperText>
            ) : null}
          </>
        ) : null}
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 2, width: '100%' }}>
          <Button disabled={saving || credentialRequired} sx={{ minWidth: 100 }} variant="contained" onClick={save}>
            {translate(flowiseI18nKeys.common.save)}
          </Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

function SpeechToTextInput({
  credentialOptions,
  input,
  value,
  onChange
}: {
  credentialOptions: FlowiseResourceDto[];
  input: SpeechToTextInputDefinition;
  value: unknown;
  onChange: (value: unknown) => void;
}) {
  const { translate } = useI18n();

  if (input.type === 'credential') {
    return (
      <FormControl fullWidth size="small">
        <InputLabel>{translate(flowiseI18nKeys.configuration.credential)}</InputLabel>
        <Select label={translate(flowiseI18nKeys.configuration.credential)} value={String(value || '')} onChange={(event) => onChange(event.target.value)}>
          {credentialOptions.map((credential) => (
            <MenuItem key={credential.id} value={credential.id}>
              {credential.displayName}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
    );
  }

  if (input.type === 'options') {
    return (
      <FormControl fullWidth size="small">
        <InputLabel>{translate(input.labelKey)}</InputLabel>
        <Select label={translate(input.labelKey)} value={String(value || input.defaultValue || '')} onChange={(event) => onChange(event.target.value)}>
          {(input.options ?? []).map((option) => (
            <MenuItem key={option.value} value={option.value}>
              {translate(option.labelKey)}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
    );
  }

  return (
    <TextField
      fullWidth
      multiline={Boolean(input.rows)}
      placeholder={input.placeholder}
      rows={input.rows}
      size="small"
      type={input.type === 'number' ? 'number' : 'text'}
      value={String(value ?? '')}
      slotProps={input.type === 'number' ? { htmlInput: { step: input.step } } : undefined}
      onChange={(event) => onChange(input.type === 'number' ? numberOrText(event.target.value) : event.target.value)}
    />
  );
}

function credentialInput(credentialNames: string[]): SpeechToTextInputDefinition {
  return {
    credentialNames,
    labelKey: flowiseI18nKeys.configuration.credential,
    name: 'credentialId',
    type: 'credential'
  };
}

function textInput(name: string, labelKey: string, descriptionKey?: string, placeholder?: string, optional?: boolean, rows?: number): SpeechToTextInputDefinition {
  return { descriptionKey, labelKey, name, optional, placeholder, rows, type: 'string' };
}

function numberInput(name: string, labelKey: string, descriptionKey?: string, optional?: boolean, step?: number): SpeechToTextInputDefinition {
  return { descriptionKey, labelKey, name, optional, step, type: 'number' };
}

function parseSpeechToTextConfig(jsonValue: string): SpeechToTextConfig {
  if (!jsonValue) {
    return {};
  }

  try {
    const parsed = JSON.parse(jsonValue) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as SpeechToTextConfig : {};
  } catch {
    return {};
  }
}

function resolveSelectedProvider(config: SpeechToTextConfig): SpeechToTextProviderName {
  for (const key of Object.keys(speechToTextProviders) as Array<Exclude<SpeechToTextProviderName, 'none'>>) {
    if (config[key]?.status) {
      return key;
    }
  }

  return config.none?.status ? 'none' : 'none';
}

function markProviderActive(config: SpeechToTextConfig, providerName: SpeechToTextProviderName): SpeechToTextConfig {
  const next: SpeechToTextConfig = {
    ...config,
    [providerName]: {
      ...(config[providerName] ?? {}),
      status: true
    }
  };

  for (const key of Object.keys(speechToTextProviders)) {
    if (key !== providerName) {
      next[key] = { ...(config[key] ?? {}), status: false };
    }
  }

  if (providerName !== 'none') {
    next.none = { ...(config.none ?? {}), status: false };
  }

  return next;
}

function filterCredentials(credentials: FlowiseResourceDto[], credentialNames: string[]) {
  if (credentialNames.length === 0) {
    return credentials;
  }

  return credentials.filter((credential) => {
    const metadata = parseSpeechToTextConfig(credential.metadataJson);
    const definition = parseSpeechToTextConfig(credential.definitionJson);
    const candidateValues = [
      credential.resourceKey,
      credential.category,
      metadata.credentialName,
      metadata.credentialType,
      definition.credentialName,
      definition.credentialType
    ].map((value) => String(value ?? '').trim()).filter(Boolean);

    return candidateValues.length === 0 || candidateValues.some((value) => credentialNames.includes(value));
  });
}

function numberOrText(value: string) {
  return value.trim() === '' ? '' : Number(value);
}
