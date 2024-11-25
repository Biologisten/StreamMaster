import SMPopUp, { SMPopUpRef } from '@components/sm/SMPopUp';
import { Logger } from '@lib/common/logger';
import { RemoveStreamGroupProfile } from '@lib/smAPI/StreamGroups/StreamGroupsCommands';

import { RemoveStreamGroupProfileRequest, StreamGroupProfile } from '@lib/smAPI/smapiTypes';
import { useCallback, useRef } from 'react';

interface RemoveStreamGroupProfileDialogProps {
  streamGroupProfile: StreamGroupProfile;
}

const RemoveStreamGroupProfileDialog = ({ ...props }: RemoveStreamGroupProfileDialogProps) => {
  const smPopUpRef = useRef<SMPopUpRef>(null);
  const remove = useCallback(() => {
    const request = {
      ProfileName: props.streamGroupProfile.ProfileName,
      StreamGroupId: props.streamGroupProfile.StreamGroupId
    } as RemoveStreamGroupProfileRequest;

    RemoveStreamGroupProfile(request)
      .then((response) => {})
      .catch((error) => {
        Logger.error('RemoveStreamGroupProfileDialog', { error });
      })
      .finally(() => {
        smPopUpRef.current?.hide();
      });
  }, [props]);

  return (
    <SMPopUp
      contentWidthSize="2"
      buttonDisabled={!props.streamGroupProfile.ProfileName || props.streamGroupProfile.ProfileName.toLowerCase() === 'default'}
      buttonClassName="icon-red"
      icon="pi-times"
      info=""
      title="Remove Profile?"
      modal
      modalCentered
      onOkClick={() => {
        remove();
      }}
      ref={smPopUpRef}
      okButtonDisabled={!props.streamGroupProfile.ProfileName || props.streamGroupProfile.ProfileName.toLowerCase() === 'default'}
      tooltip="Remove Profile"
      zIndex={10}
    >
      <div className="sm-center-stuff">
        <div className="text-container"> {props.streamGroupProfile.ProfileName}</div>
      </div>
    </SMPopUp>
  );
};

export default RemoveStreamGroupProfileDialog;
